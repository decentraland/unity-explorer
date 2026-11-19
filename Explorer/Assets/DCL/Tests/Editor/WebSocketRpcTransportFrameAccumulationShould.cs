using Cysharp.Threading.Tasks;
using DCL.SocialService;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utility.Networking;

namespace DCL.Tests.Editor
{
    [TestFixture]
    public class WebSocketRpcTransportFrameAccumulationShould
    {
        private const int TIMEOUT_MS = 10_000;
        private const int BUFFER_SIZE = 256;

        [Test]
        public async Task DispatchSingleFrameMessageUnchanged()
        {
            byte[] payload = { 0xDE, 0xAD, 0xBE, 0xEF };

            await RunTransportTestAsync(async (server, transport, messages, errors) =>
            {
                await SendBinaryFramesAsync(server, payload, fragmentCount: 1);
                await WaitForMessagesAsync(messages, expectedCount: 1);

                Assert.That(messages.Count, Is.EqualTo(1));
                Assert.That(messages.First(), Is.EqualTo(payload));
            });
        }

        [Test]
        public async Task ReassembleMultiFrameMessageIntoOneCallback()
        {
            byte[] payload = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };

            await RunTransportTestAsync(async (server, transport, messages, errors) =>
            {
                await SendBinaryFramesAsync(server, payload, fragmentCount: 4);
                await WaitForMessagesAsync(messages, expectedCount: 1);

                Assert.That(messages.Count, Is.EqualTo(1));
                Assert.That(messages.First(), Is.EqualTo(payload));
            });
        }

        [Test]
        public async Task DeliverExactBufferCapacityMessageSuccessfully()
        {
            byte[] payload = new byte[BUFFER_SIZE];
            for (int i = 0; i < payload.Length; i++)
                payload[i] = (byte)(i % 251);

            await RunTransportTestAsync(async (server, transport, messages, errors) =>
            {
                await SendBinaryFramesAsync(server, payload, fragmentCount: 2);
                await WaitForMessagesAsync(messages, expectedCount: 1);

                Assert.That(messages.Count, Is.EqualTo(1));
                Assert.That(messages.First(), Is.EqualTo(payload));
            });
        }

        [Test]
        public async Task RaiseErrorWithoutMessageCallbackWhenMessageExceedsBuffer()
        {
            byte[] payload = new byte[BUFFER_SIZE + 64];
            for (int i = 0; i < payload.Length; i++)
                payload[i] = (byte)(i % 251);

            await RunTransportTestAsync(async (server, transport, messages, errors) =>
            {
                await SendBinaryFramesAsync(server, payload, fragmentCount: 2);
                await WaitForErrorsAsync(errors, expectedCount: 1);

                Assert.That(messages.Count, Is.Zero, "No message callback should fire for an oversized payload");
                Assert.That(errors.Count, Is.EqualTo(1), "Exactly one error should be raised");
            });
        }

        [Test]
        public async Task ContinueReceivingAfterHandlerException()
        {
            byte[] first = { 0xAA, 0xBB };
            byte[] second = { 0xCC, 0xDD };
            var callCount = 0;

            await RunTransportTestAsync(async (server, transport, messages, errors) =>
            {
                transport.OnMessageEvent += _ =>
                {
                    if (Interlocked.Increment(ref callCount) == 1)
                        throw new InvalidOperationException("Simulated handler failure");
                };

                await SendBinaryFramesAsync(server, first, fragmentCount: 1);
                await WaitForConditionAsync(() => callCount >= 1);

                await SendBinaryFramesAsync(server, second, fragmentCount: 1);
                await WaitForConditionAsync(() => callCount >= 2);

                Assert.That(callCount, Is.GreaterThanOrEqualTo(2),
                    "The receive loop must survive a handler exception and dispatch the next message");
            });
        }

        private static async Task RunTransportTestAsync(
            Func<NetworkStream, WebSocketRpcTransport, ConcurrentQueue<byte[]>, ConcurrentQueue<Exception>, Task> testBody)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var messages = new ConcurrentQueue<byte[]>();
            var errors = new ConcurrentQueue<Exception>();

            var transport = new WebSocketRpcTransport(new Uri($"ws://127.0.0.1:{port}/"), BUFFER_SIZE);
            transport.OnMessageEvent += data => messages.Enqueue(data);
            transport.OnErrorEvent += ex => errors.Enqueue(ex);

            Task<Socket> acceptTask = listener.AcceptSocketAsync();

            await transport.ConnectAsync(CancellationToken.None);

            Socket serverSocket = await AwaitWithTimeoutAsync(acceptTask, "server accept");
            var serverStream = new NetworkStream(serverSocket, ownsSocket: true);

            await CompleteWebSocketUpgradeAsync(serverStream);

            transport.ListenForIncomingData();

            try { await testBody(serverStream, transport, messages, errors); }
            finally
            {
                transport.Dispose();
                serverStream.Dispose();
                listener.Stop();
            }
        }

        private static async Task CompleteWebSocketUpgradeAsync(NetworkStream stream)
        {
            string request = await ReadHttpHeadersAsync(stream);
            string key = string.Empty;

            foreach (string line in request.Split(new[] { "\r\n" }, StringSplitOptions.None))
            {
                if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                    key = line.Substring("Sec-WebSocket-Key:".Length).Trim();
            }

            string acceptHash;

            using (SHA1 sha1 = SHA1.Create())
                acceptHash = Convert.ToBase64String(sha1.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

            byte[] response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: websocket\r\nSec-WebSocket-Accept: {acceptHash}\r\n\r\n");

            await stream.WriteAsync(response, 0, response.Length);
        }

        private static async Task SendBinaryFramesAsync(NetworkStream stream, byte[] payload, int fragmentCount)
        {
            int chunkSize = Math.Max(1, payload.Length / fragmentCount);

            for (int offset = 0; offset < payload.Length;)
            {
                int remaining = payload.Length - offset;
                int thisChunk = Math.Min(chunkSize, remaining);
                bool isFirst = offset == 0;
                bool isLast = offset + thisChunk >= payload.Length;

                byte opcode = isFirst ? (byte)0x02 : (byte)0x00;
                byte fin = isLast ? (byte)0x80 : (byte)0x00;

                byte[] frame = BuildWebSocketFrame((byte)(fin | opcode), payload, offset, thisChunk);
                await stream.WriteAsync(frame, 0, frame.Length);
                await stream.FlushAsync();

                offset += thisChunk;
            }
        }

        private static byte[] BuildWebSocketFrame(byte firstByte, byte[] payload, int offset, int length)
        {
            byte[] header;

            if (length < 126)
            {
                header = new byte[] { firstByte, (byte)length };
            }
            else if (length < 65536)
            {
                header = new byte[]
                {
                    firstByte, 126,
                    (byte)(length >> 8),
                    (byte)(length & 0xFF)
                };
            }
            else
            {
                header = new byte[]
                {
                    firstByte, 127,
                    0, 0, 0, 0,
                    (byte)((length >> 24) & 0xFF),
                    (byte)((length >> 16) & 0xFF),
                    (byte)((length >> 8) & 0xFF),
                    (byte)(length & 0xFF)
                };
            }

            byte[] frame = new byte[header.Length + length];
            Buffer.BlockCopy(header, 0, frame, 0, header.Length);
            Buffer.BlockCopy(payload, offset, frame, header.Length, length);
            return frame;
        }

        private static async Task<string> ReadHttpHeadersAsync(NetworkStream stream)
        {
            var sb = new StringBuilder();
            var buf = new byte[1];

            while (!HeadersComplete(sb))
            {
                int read = await stream.ReadAsync(buf, 0, 1);

                if (read == 0)
                    throw new IOException("Connection closed before HTTP upgrade completed");

                sb.Append((char)buf[0]);
            }

            return sb.ToString();
        }

        private static bool HeadersComplete(StringBuilder sb) =>
            sb.Length >= 4
            && sb[sb.Length - 4] == '\r' && sb[sb.Length - 3] == '\n'
            && sb[sb.Length - 2] == '\r' && sb[sb.Length - 1] == '\n';

        private static async Task WaitForMessagesAsync(ConcurrentQueue<byte[]> messages, int expectedCount)
        {
            await WaitForConditionAsync(() => messages.Count >= expectedCount);
        }

        private static async Task WaitForErrorsAsync(ConcurrentQueue<Exception> errors, int expectedCount)
        {
            await WaitForConditionAsync(() => errors.Count >= expectedCount);
        }

        private static async Task WaitForConditionAsync(Func<bool> condition)
        {
            Task completed = await Task.WhenAny(
                Task.Run(async () =>
                {
                    while (!condition())
                        await Task.Delay(10);
                }),
                Task.Delay(TIMEOUT_MS));

            if (completed.IsFaulted)
                await completed;
        }

        private static async Task<T> AwaitWithTimeoutAsync<T>(Task<T> task, string description)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(TIMEOUT_MS));
            Assert.That(completed, Is.SameAs(task), $"{description} did not complete within {TIMEOUT_MS}ms");
            return await task;
        }
    }
}
