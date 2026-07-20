using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
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
    ///     POST carries a single JSON-RPC message; GET (server-initiated stream) is not supported and returns 405.
    /// </summary>
    public class McpHttpServer : IDisposable
    {
        private const int MAX_BODY_BYTES = 1024 * 1024;

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
