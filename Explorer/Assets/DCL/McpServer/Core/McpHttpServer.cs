using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
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
    ///     POST carries a single JSON-RPC message; GET opens the server-to-client SSE stream — a log-stream
    ///     subscription (<c>?stream=scene|client</c>, optional <c>?level=</c>) delivered via <see cref="McpNotificationChannel" />.
    /// </summary>
    public class McpHttpServer : IDisposable
    {
        private const int MAX_BODY_BYTES = 1024 * 1024;

        // Single source of truth for the endpoint path. Docs and scripts restate it (they cannot reference this
        // const): docs/mcp-automation.md, docs/app-arguments.md, .claude/skills/mcp-scene-iteration/ — keep in sync.
        private const string ENDPOINT_PATH = "unity-explorer-mcp";

        private readonly McpJsonRpcDispatcher dispatcher;
        private readonly int port;
        private readonly McpNotificationChannel notifications;

        // The session maps to the Explorer process, not to a client: this server is stateless and drives one
        // shared world, so every request operates on the same state and there is nothing session-scoped to isolate.
        // Multiple agents on one server intentionally share this id because they drive the same world; a separate
        // session means a separate Explorer instance (separate port, separate id). Hence one id for the process
        // lifetime, echoed on every response, and no need to validate the incoming Mcp-Session-Id.
        private readonly string sessionId = Guid.NewGuid().ToString("N");

        private HttpListener? listener;

        /// <summary>The localhost URL the server listens on, e.g. <c>http://127.0.0.1:8123/unity-explorer-mcp</c>.</summary>
        public string EndpointUrl => $"http://127.0.0.1:{port}/{ENDPOINT_PATH}";

        public McpHttpServer(McpToolsRegistry toolsRegistry, int port, McpNotificationChannel notifications)
        {
            this.dispatcher = new McpJsonRpcDispatcher(toolsRegistry, Application.version);
            this.port = port;
            this.notifications = notifications;
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
                    case "GET":
                        await HandleGetAsync(context, ct);
                        break;
                    case "DELETE":
                        // Session termination is accepted but stateless: nothing to clean up.
                        context.Response.WriteEmptyAndClose(HttpStatusCode.OK, sessionId);
                        break;
                    default:
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
            if (context.Request.ContentLength64 > MAX_BODY_BYTES)
            {
                context.Response.WriteEmptyAndClose(HttpStatusCode.RequestEntityTooLarge, sessionId);
                return;
            }

            string requestJson;

            // The Content-Length check above is only a fast reject; a chunked request reports ContentLength64 == -1
            // and bypasses it, so cap the read itself into a fixed buffer to keep the body bounded regardless.
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                var buffer = new char[MAX_BODY_BYTES + 1];
                int charsRead = await reader.ReadBlockAsync(buffer, 0, buffer.Length);

                if (charsRead > MAX_BODY_BYTES)
                {
                    context.Response.WriteEmptyAndClose(HttpStatusCode.RequestEntityTooLarge, sessionId);
                    return;
                }

                requestJson = new string(buffer, 0, charsRead);
            }

            string? responseJson = await dispatcher.DispatchAsync(requestJson, ct);

            if (responseJson == null)
            {
                // Notifications get 202 Accepted with no body.
                context.Response.WriteEmptyAndClose(HttpStatusCode.Accepted, sessionId);
                return;
            }

            byte[] payload = Encoding.UTF8.GetBytes(responseJson);

            context.Response.WithMcpHeaders(sessionId);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = payload.Length;
            await context.Response.OutputStream.WriteAsync(payload, 0, payload.Length, CancellationToken.None);
            context.Response.Close();
        }

        private async UniTask HandleGetAsync(HttpListenerContext context, CancellationToken ct)
        {
            // The standalone server-to-client SSE stream: subscribe to one log stream and receive
            // notifications/message events until the client disconnects or the server stops.
            string? accept = context.Request.Headers["Accept"];

            if (accept == null || !accept.Contains("text/event-stream"))
            {
                context.Response.WriteEmptyAndClose(HttpStatusCode.MethodNotAllowed, sessionId);
                return;
            }

            string? stream = context.Request.QueryString["stream"];

            if (!McpLogStreams.IsKnown(stream))
            {
                WriteBadRequest(context, "GET requires ?stream=scene or ?stream=client");
                return;
            }

            var minLevel = McpLogLevel.Debug;
            string? levelParam = context.Request.QueryString["level"];

            if (!string.IsNullOrEmpty(levelParam) && !McpLogLevelExtensions.TryParse(levelParam, out minLevel))
            {
                WriteBadRequest(context, $"Unknown level '{levelParam}' (RFC 5424: debug..emergency)");
                return;
            }

            HttpListenerResponse response = context.Response;
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "text/event-stream";
            response.SendChunked = true;
            response.AddHeader("Cache-Control", "no-cache");
            response.WithMcpHeaders(sessionId);

            // A vanished client is only discovered when a write fails; the sink then cancels this token so the
            // handler stops holding the connection instead of parking on ct until the server shuts down.
            using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            IDisposable? subscription = notifications.Add(stream!, minLevel, new HttpListenerSseSink(response, connectionCts));

            if (subscription == null)
            {
                // The channel is shutting down.
                TryAbort(context);
                return;
            }

            try
            {
                // Park until the connection token cancels (server shutdown or a write-failure via the sink).
                var closed = new UniTaskCompletionSource();
                using (connectionCts.Token.Register(() => closed.TrySetResult()))
                    await closed.Task;
            }
            finally { subscription.Dispose(); }
        }

        private void WriteBadRequest(HttpListenerContext context, string message)
        {
            byte[] payload = Encoding.UTF8.GetBytes(message);
            context.Response.WithMcpHeaders(sessionId);
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.ContentLength64 = payload.Length;
            context.Response.OutputStream.Write(payload, 0, payload.Length);
            context.Response.Close();
        }

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

        /// <summary>Writes SSE chunks to one still-open HTTP response; serialized, and self-latching once broken.</summary>
        private sealed class HttpListenerSseSink : McpNotificationChannel.ISseSink
        {
            private readonly HttpListenerResponse response;
            private readonly CancellationTokenSource connectionCts;
            private readonly object gate = new ();
            private bool broken;

            public HttpListenerSseSink(HttpListenerResponse response, CancellationTokenSource connectionCts)
            {
                this.response = response;
                this.connectionCts = connectionCts;
            }

            public bool TryWrite(byte[] bytes)
            {
                lock (gate)
                {
                    if (broken) return false;

                    try
                    {
                        response.OutputStream.Write(bytes, 0, bytes.Length);
                        response.OutputStream.Flush();
                        return true;
                    }
                    catch (Exception)
                    {
                        broken = true;
                        return false;
                    }
                }
            }

            public void Close()
            {
                lock (gate)
                {
                    broken = true;
                    try { response.Close(); }
                    catch (Exception) { /* already torn down */ }
                }

                // Ends the parked GET handler. The linked CTS may already be disposed if that handler
                // completed first (server shutdown), so tolerate it.
                try { connectionCts.Cancel(); }
                catch (ObjectDisposedException) { }
            }
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
