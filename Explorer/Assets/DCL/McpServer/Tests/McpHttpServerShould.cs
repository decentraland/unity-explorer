using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using NUnit.Framework;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace DCL.McpServer.Tests
{
    /// <summary>
    ///     Covers <c>McpHttpServer.IsAllowed</c>, the DNS-rebinding guard that decides which Origin headers may
    ///     reach the JSON-RPC dispatcher. The method is private static and lives in Core, which these tests do
    ///     not modify, so it is exercised through reflection. If IsAllowed is later exposed as internal
    ///     (with InternalsVisibleTo), replace the reflection shim with a direct call.
    /// </summary>
    public class McpHttpServerShould
    {
        private static readonly MethodInfo IS_ALLOWED =
            typeof(McpHttpServer).GetMethod("IsAllowed", BindingFlags.NonPublic | BindingFlags.Static)!;

        private static bool IsAllowed(string? origin) =>
            (bool)IS_ALLOWED.Invoke(null, new object?[] { origin })!;

        [TestCase(null)]
        [TestCase("")]
        public void AllowRequestsWithoutAnOrigin(string? origin) =>
            Assert.That(IsAllowed(origin), Is.True);

        [TestCase("http://localhost")]
        [TestCase("http://localhost:8080")]
        [TestCase("http://127.0.0.1")]
        [TestCase("http://127.0.0.1:9001")]
        [TestCase("https://127.0.0.1:9001")]
        public void AllowLoopbackHttpOrigins(string origin) =>
            Assert.That(IsAllowed(origin), Is.True);

        [TestCase("http://evil.com")]
        [TestCase("https://attacker.example:9001")]
        [TestCase("http://127.0.0.1.evil.com")] // rebinding: a foreign host that merely starts with the loopback IP
        public void RejectNonLoopbackHosts(string origin) =>
            Assert.That(IsAllowed(origin), Is.False);

        [TestCase("ftp://127.0.0.1")]
        [TestCase("file:///etc/passwd")]
        public void RejectNonHttpSchemes(string origin) =>
            Assert.That(IsAllowed(origin), Is.False);

        [TestCase("not-a-valid-origin")]
        [TestCase("://missing-scheme")]
        public void RejectMalformedOrigins(string origin) =>
            Assert.That(IsAllowed(origin), Is.False);
    }

    /// <summary>
    ///     Drives a real <see cref="McpHttpServer" /> over raw TCP to exercise the transport body-read path end
    ///     to end: the Content-Length contract (411/413/400), the drain-before-reject that keeps those statuses
    ///     from surfacing as a client-side connection reset, the MCP-Protocol-Version guard, the GET → 405 rule
    ///     and the loopback Host match. These paths run entirely on the thread pool (no tool is invoked), so no
    ///     Unity player loop is needed. The client uses <c>Connection: close</c> so each response reads to EOF.
    /// </summary>
    public class McpHttpServerTransportShould
    {
        private const string PING = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}";
        private const string PATH = "/unity-explorer-mcp";

        // MAX_BODY_BYTES in the server is 1 MiB; 2 MiB is safely over the cap yet under the 8 MiB drain cap, so
        // the whole oversized body is consumed and the rejection is delivered as a status, not a reset.
        private const int OVERSIZED_BODY_BYTES = 2 * 1024 * 1024;

        private McpHttpServer server = null!;
        private CancellationTokenSource cts = null!;
        private int port;

        [SetUp]
        public void SetUp()
        {
            port = FreeLoopbackPort();

            var registry = new McpToolsRegistry();
            registry.Build();

            server = new McpHttpServer(registry, port);
            cts = new CancellationTokenSource();

            Assert.That(server.TryStart(), Is.True, "the test server failed to start");
            server.RunAsync(cts.Token).Forget();
        }

        [TearDown]
        public void TearDown()
        {
            cts.Cancel();
            server.Dispose();
            cts.Dispose();
        }

        [Test]
        public void AnswerAWellFormedPostWith200AndAJsonRpcResult()
        {
            string response = SendRaw(Post(PING));

            Assert.That(StatusLine(response), Does.StartWith("HTTP/1.1 200"));
            Assert.That(response, Does.Contain("\"jsonrpc\":\"2.0\""));
            Assert.That(response, Does.Contain("\"result\""));

            // The protocol version is echoed only in the MCP-Protocol-Version response header (the ping result
            // carries no version), so its presence confirms the header is written; matched value-only to stay
            // agnostic to how the HttpListener cases the header name.
            Assert.That(response, Does.Contain(McpJsonRpcDispatcher.PROTOCOL_VERSION));
        }

        [Test]
        public void RejectAChunkedBodyWith411AfterDraining()
        {
            // Transfer-Encoding: chunked reports ContentLength64 == -1; the server requires a declared length.
            string response = SendRaw(PostChunked(PING));

            Assert.That(StatusLine(response), Does.StartWith("HTTP/1.1 411"));
        }

        [Test]
        public void RejectAnOversizedBodyWith413ByExactContentLength()
        {
            byte[] request = Post(new string('a', OVERSIZED_BODY_BYTES));

            // The full body is sent; a clean 413 (not "<reset>") proves the drain-before-reject works.
            string response = SendRaw(request);

            Assert.That(StatusLine(response), Does.StartWith("HTTP/1.1 413"));
        }

        [Test]
        public void RejectATruncatedBodyWith400OnEarlyEof()
        {
            // Declare 1000 bytes but send 100, then half-close the send side so the read hits EOF early.
            byte[] request = Post(new string('a', 100), contentLengthOverride: 1000);

            string response = SendRaw(request, halfCloseAfterSend: true);

            Assert.That(StatusLine(response), Does.StartWith("HTTP/1.1 400"));
        }

        [Test]
        public void RejectAnUnsupportedProtocolVersionWith400()
        {
            byte[] request = Post(PING, protocolVersion: "1999-01-01");

            string response = SendRaw(request);

            Assert.That(StatusLine(response), Does.StartWith("HTTP/1.1 400"));
        }

        [Test]
        public void AcceptTheDeclaredProtocolVersion()
        {
            byte[] request = Post(PING, protocolVersion: McpJsonRpcDispatcher.PROTOCOL_VERSION);

            string response = SendRaw(request);

            Assert.That(StatusLine(response), Does.StartWith("HTTP/1.1 200"));
        }

        [Test]
        public void AnswerGetWith405()
        {
            string response = SendRaw(Get());

            Assert.That(StatusLine(response), Does.StartWith("HTTP/1.1 405"));
        }

        [Test]
        public void RejectAForeignHostWith400()
        {
            // The 127.0.0.1 prefix is matched literally by the HttpListener, so a foreign Host never reaches the
            // handler: Mono answers 400 itself. This is the anti-DNS-rebinding guarantee we rely on instead of an
            // explicit host allow-list; the assertion pins it so a regression in that behaviour is caught here.
            byte[] request = Post(PING, host: $"evil.example:{port}");

            string response = SendRaw(request);

            Assert.That(StatusLine(response), Does.StartWith("HTTP/1.1 400"));
        }

        private byte[] Post(string body, string? host = null, string? protocolVersion = null, int? contentLengthOverride = null)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

            var head = new StringBuilder();
            head.Append("POST ").Append(PATH).Append(" HTTP/1.1\r\n");
            head.Append("Host: ").Append(host ?? $"127.0.0.1:{port}").Append("\r\n");
            head.Append("Content-Type: application/json\r\n");
            head.Append("Connection: close\r\n");

            if (protocolVersion != null)
                head.Append("MCP-Protocol-Version: ").Append(protocolVersion).Append("\r\n");

            head.Append("Content-Length: ").Append(contentLengthOverride ?? bodyBytes.Length).Append("\r\n\r\n");

            return Concat(Encoding.ASCII.GetBytes(head.ToString()), bodyBytes);
        }

        private byte[] PostChunked(string body)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

            var head = new StringBuilder();
            head.Append("POST ").Append(PATH).Append(" HTTP/1.1\r\n");
            head.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");
            head.Append("Content-Type: application/json\r\n");
            head.Append("Connection: close\r\n");
            head.Append("Transfer-Encoding: chunked\r\n\r\n");

            var chunked = new StringBuilder();
            chunked.Append(bodyBytes.Length.ToString("x")).Append("\r\n").Append(body).Append("\r\n0\r\n\r\n");

            return Concat(Encoding.ASCII.GetBytes(head.ToString()), Encoding.ASCII.GetBytes(chunked.ToString()));
        }

        private byte[] Get()
        {
            var head = new StringBuilder();
            head.Append("GET ").Append(PATH).Append(" HTTP/1.1\r\n");
            head.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");
            head.Append("Connection: close\r\n\r\n");

            return Encoding.ASCII.GetBytes(head.ToString());
        }

        /// <summary>
        ///     Sends the raw request and returns the full response text, or the sentinel <c>"&lt;reset&gt;"</c>
        ///     when the server resets the connection instead of answering (the failure the drain guards against).
        /// </summary>
        private string SendRaw(byte[] request, bool halfCloseAfterSend = false)
        {
            using var client = new TcpClient { SendTimeout = 10_000, ReceiveTimeout = 10_000 };
            client.Connect(IPAddress.Loopback, port);

            try
            {
                NetworkStream stream = client.GetStream();
                stream.Write(request, 0, request.Length);

                if (halfCloseAfterSend)
                    client.Client.Shutdown(SocketShutdown.Send);

                var response = new StringBuilder();
                var buffer = new byte[4096];
                int read;

                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    response.Append(Encoding.ASCII.GetString(buffer, 0, read));

                return response.Length == 0 ? "<closed>" : response.ToString();
            }
            catch (Exception e) when (e is IOException or SocketException)
            {
                return "<reset>";
            }
        }

        private static string StatusLine(string response)
        {
            int eol = response.IndexOf('\r');
            return eol > 0 ? response.Substring(0, eol) : response;
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            var result = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            return result;
        }

        private static int FreeLoopbackPort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int freePort = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return freePort;
        }
    }
}
