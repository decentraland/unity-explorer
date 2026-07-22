using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.McpServer.Core
{
    /// <summary>
    ///     Minimal MCP Streamable HTTP transport on top of <see cref="HttpListener" />, bound to 127.0.0.1 only.
    ///     POST carries a single JSON-RPC message; GET (server-initiated stream) is not supported and returns 405.
    /// </summary>
    public class McpHttpServer : IDisposable
    {
        private const int MAX_BODY_BYTES = 1024 * 1024;

        // HttpListener RSTs the connection when the response closes while the client is still uploading, so an
        // early rejection (411/413/400) must first consume the pending body or the client sees ECONNRESET instead
        // of the status. This caps how much of an oversized/refused body is read before the rejection is sent.
        private const int DRAIN_CAP_BYTES = 8 * 1024 * 1024;

        private readonly McpJsonRpcDispatcher dispatcher;
        private readonly int port;

        // The session maps to the Explorer process, not to a client: this server is stateless and drives one
        // shared world, so every request operates on the same state and there is nothing session-scoped to isolate.
        // Multiple agents on one server intentionally share this id because they drive the same world; a separate
        // session means a separate Explorer instance (separate port, separate id). Hence one id for the process
        // lifetime, echoed on every response, and no need to validate the incoming Mcp-Session-Id.
        private readonly string sessionId = Guid.NewGuid().ToString("N");

        private HttpListener? listener;

        /// <summary>The localhost URL the server listens on, e.g. <c>http://127.0.0.1:8123/unity-explorer-mcp</c>.</summary>
        public string EndpointUrl { get; }

        public McpHttpServer(McpToolsRegistry toolsRegistry, int port)
        {
            this.dispatcher = new McpJsonRpcDispatcher(toolsRegistry, Application.version);
            this.port = port;
            EndpointUrl = string.Format(IDecentralandUrlsSource.LOCAL_MCP_ENDPOINT_URL, port);
        }

        public void Dispose()
        {
            if (listener == null) return;

            try
            {
                listener.Stop();
                listener.Close();
            }
            catch (ObjectDisposedException) { }

            listener = null;
        }

        public bool TryStart()
        {
            var newListener = new HttpListener();
            newListener.Prefixes.Add($"{EndpointUrl}/"); // HttpListener requires the trailing slash

            try { newListener.Start(); }
            catch (Exception e) when (e is HttpListenerException or InvalidOperationException)
            {
                ReportHub.LogError(ReportCategory.MCP, $"Cannot start the MCP server on port {port} (already in use? pass a different --mcp-port): {e.Message}");
                newListener.Close();
                return false;
            }

            listener = newListener;
            ReportHub.Log(LogType.Log, ReportCategory.MCP, $"MCP server listening on {EndpointUrl}");
            return true;
        }

        public async UniTaskVoid RunAsync(CancellationToken ct)
        {
            await DCLTask.SwitchToThreadPool();

            // Capture once: a concurrent Dispose() on the main thread nulls the field, and re-reading it between
            // the guard and GetContextAsync would throw an unfiltered NRE. Dispose still stops/closes this same
            // instance, so a parked GetContextAsync surfaces as one of the caught exceptions below.
            HttpListener? local = listener;

            while (!ct.IsCancellationRequested && local is { IsListening: true })
            {
                HttpListenerContext context;

                try { context = await local.GetContextAsync(); }
                catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                {
                    // The listener was stopped or disposed; end the accept loop.
                    break;
                }

                HandleRequestAsync(context, ct).Forget();
            }
        }

        private async UniTaskVoid HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
        {
            // The accept loop runs this handler inline until the first suspension; hop off it before the
            // synchronous body read so one slow client cannot stall accepting new connections.
            await DCLTask.SwitchToThreadPool();

            try
            {
                if (!IsAllowed(context.Request.Headers["Origin"]))
                {
                    context.Response.WriteEmptyAndClose(HttpStatusCode.Forbidden, sessionId);
                    return;
                }

                switch (context.Request.HttpMethod)
                {
                    case "POST":
                        await HandlePostAsync(context, ct);
                        break;
                    case "DELETE":
                        // Session termination is accepted but stateless: nothing to clean up.
                        context.Response.WriteEmptyAndClose(HttpStatusCode.OK, sessionId);
                        break;
                    default:
                        // GET reaches here too: we expose no server-initiated SSE stream. Answer 405, not 404 —
                        // mcp-remote and the MCP SDKs read 405 as "this endpoint is POST-only" and stop probing,
                        // whereas a 404 makes them fail with "Failed to open SSE stream". Keep this a 405.
                        context.Response.WriteEmptyAndClose(HttpStatusCode.MethodNotAllowed, sessionId);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                TryAbort(context);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.MCP);
                TryWriteInternalError(context);
            }
        }

        private async UniTask HandlePostAsync(HttpListenerContext context, CancellationToken ct)
        {
            HttpListenerRequest request = context.Request;

            // Absent on the initialize request and on pre-2025-06-18 clients, so only an explicit unsupported
            // value is rejected. This is the transport's MUST from the 2025-06-18 spec: validate the header.
            if (!IsProtocolVersionSupported(request.Headers["MCP-Protocol-Version"]))
            {
                RejectAfterDraining(context, HttpStatusCode.BadRequest);
                return;
            }

            long contentLength = request.ContentLength64;

            switch (contentLength)
            {
                // -1 means chunked or no declared length. Every real MCP client sends a Content-Length for its JSON
                // body (Node fetch/undici, Python httpx, C# StringContent), so requiring one lets the read below rent
                // an exact-size buffer and removes the unbounded-accumulator path entirely; RFC 9112 allows the 411.
                case < 0:
                    RejectAfterDraining(context, HttpStatusCode.LengthRequired);
                    return;
                case > MAX_BODY_BYTES:
                    RejectAfterDraining(context, HttpStatusCode.RequestEntityTooLarge);
                    return;
            }

            if (!TryReadBody(request, (int)contentLength, out string requestJson))
            {
                // The client declared more than it sent and the stream hit EOF early. The body is already fully
                // consumed at EOF, so unlike the rejections above this one needs no drain before answering.
                context.Response.WriteEmptyAndClose(HttpStatusCode.BadRequest, sessionId);
                return;
            }

            string? responseJson = await dispatcher.DispatchAsync(requestJson, ct);

            if (responseJson == null)
            {
                // Notifications get 202 Accepted with no body.
                context.Response.WriteEmptyAndClose(HttpStatusCode.Accepted, sessionId);
                return;
            }

            int byteCount = Encoding.UTF8.GetByteCount(responseJson);
            byte[] payload = ArrayPool<byte>.Shared.Rent(byteCount);

            try
            {
                Encoding.UTF8.GetBytes(responseJson, 0, responseJson.Length, payload, 0);

                context.Response.WithMcpHeaders(sessionId);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = byteCount;
                await context.Response.OutputStream.WriteAsync(payload, 0, byteCount, CancellationToken.None);
                context.Response.Close();
            }
            finally { ArrayPool<byte>.Shared.Return(payload); }
        }

        /// <summary>
        ///     Reads exactly <paramref name="length" /> bytes of the request body synchronously (blocking the
        ///     current thread-pool thread) into a pooled buffer, so the per-request heap cost is only the final
        ///     string. Returns false when the stream ends before <paramref name="length" /> bytes arrive;
        ///     <paramref name="body" /> is then empty and the request must be rejected as a 400.
        /// </summary>
        private static bool TryReadBody(HttpListenerRequest request, int length, out string body)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(length);

            try
            {
                using Stream input = request.InputStream;

                int offset = 0;

                while (offset < length)
                {
                    int read = input.Read(rented, offset, length - offset);

                    if (read == 0)
                    {
                        // EOF before the declared Content-Length: a truncated or lying client.
                        body = string.Empty;
                        return false;
                    }

                    offset += read;
                }

                body = Encoding.UTF8.GetString(rented, 0, length);
                return true;
            }
            finally { ArrayPool<byte>.Shared.Return(rented); }
        }

        private void RejectAfterDraining(HttpListenerContext context, HttpStatusCode status)
        {
            DrainRequestBody(context.Request);
            context.Response.WriteEmptyAndClose(status, sessionId);
        }

        /// <summary>
        ///     Consumes up to <see cref="DRAIN_CAP_BYTES" /> of a body we are about to reject, so closing the
        ///     response does not RST the connection and cost the client its status (verified on Mono's
        ///     HttpListener). A client that keeps sending past the cap still gets reset — an acceptable outcome
        ///     for a flood far larger than the accepted body.
        /// </summary>
        private static void DrainRequestBody(HttpListenerRequest request)
        {
            byte[] chunk = ArrayPool<byte>.Shared.Rent(16 * 1024);

            try
            {
                using Stream input = request.InputStream;

                long total = 0;
                int read;

                while (total < DRAIN_CAP_BYTES && (read = input.Read(chunk, 0, chunk.Length)) > 0)
                    total += read;
            }
            catch (Exception)
            {
                // The client already vanished or the body errored mid-drain; the rejection is best-effort.
            }
            finally { ArrayPool<byte>.Shared.Return(chunk); }
        }

        private static bool IsProtocolVersionSupported(string? headerValue) =>
            string.IsNullOrEmpty(headerValue) || headerValue == McpJsonRpcDispatcher.PROTOCOL_VERSION;

        private void TryWriteInternalError(HttpListenerContext context)
        {
            try
            {
                context.Response.WriteEmptyAndClose(HttpStatusCode.InternalServerError, sessionId);
            }
            catch (Exception)
            {
                // The response may already be closed or the client gone; nothing else to do.
            }
        }

        private static void TryAbort(HttpListenerContext context)
        {
            try { context.Response.Abort(); }
            catch (Exception)
            {
                // Ignored: aborting a torn-down connection during shutdown.
            }
        }

        private static bool IsAllowed(string? origin)
        {
            if (string.IsNullOrEmpty(origin))
                return true;

            if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? originUri))
                return false;

            if (originUri.Scheme != Uri.UriSchemeHttp && originUri.Scheme != Uri.UriSchemeHttps)
                return false;

            return originUri.Host is "localhost" or "127.0.0.1" or "::1";
        }
    }

    internal static class HttpListenerResponseExtensions
    {
        public static void WriteEmptyAndClose(this HttpListenerResponse response, HttpStatusCode status, string sessionId)
        {
            response.StatusCode = (int)status;
            response.ContentLength64 = 0;
            response.WithMcpHeaders(sessionId)
                    .Close();
        }

        public static HttpListenerResponse WithMcpHeaders(this HttpListenerResponse response, string sessionId)
        {
            response.AddHeader("Mcp-Session-Id", sessionId);
            response.AddHeader("MCP-Protocol-Version", McpJsonRpcDispatcher.PROTOCOL_VERSION);
            return response;
        }
    }
}
