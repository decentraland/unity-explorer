using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Mcp.Protocol;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.Mcp.Transport
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
        private readonly string sessionId = Guid.NewGuid().ToString("N");

        private HttpListener? listener;

        public McpHttpServer(McpJsonRpcDispatcher dispatcher, int port)
        {
            this.dispatcher = dispatcher;
            this.port = port;
        }

        public void Dispose()
        {
            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch (ObjectDisposedException) { }

            listener = null;
        }

        public bool TryStart()
        {
            var newListener = new HttpListener();
            newListener.Prefixes.Add($"http://127.0.0.1:{port}/mcp/");

            try { newListener.Start(); }
            catch (Exception e) when (e is HttpListenerException or InvalidOperationException)
            {
                ReportHub.LogError(ReportCategory.MCP, $"Cannot start the MCP server on port {port} (already in use? pass a different --mcp-port): {e.Message}");
                newListener.Close();
                return false;
            }

            listener = newListener;
            ReportHub.Log(LogType.Log, ReportCategory.MCP, $"MCP server listening on http://127.0.0.1:{port}/mcp");
            return true;
        }

        public async UniTaskVoid RunAsync(CancellationToken ct)
        {
            await DCLTask.SwitchToThreadPool();

            while (!ct.IsCancellationRequested && listener is { IsListening: true })
            {
                HttpListenerContext context;

                try { context = await listener.GetContextAsync(); }
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
                if (!McpOriginValidator.IsAllowed(context.Request.Headers["Origin"]))
                {
                    WriteEmpty(context.Response, (int)HttpStatusCode.Forbidden);
                    return;
                }

                switch (context.Request.HttpMethod)
                {
                    case "POST":
                        await HandlePostAsync(context, ct);
                        break;
                    case "DELETE":
                        // Session termination is accepted but stateless: nothing to clean up.
                        WriteEmpty(context.Response, (int)HttpStatusCode.OK);
                        break;
                    default:
                        WriteEmpty(context.Response, (int)HttpStatusCode.MethodNotAllowed);
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
                WriteEmpty(context.Response, (int)HttpStatusCode.RequestEntityTooLarge);
                return;
            }

            string requestJson;

            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                requestJson = await reader.ReadToEndAsync();

            string? responseJson = await dispatcher.DispatchAsync(requestJson, ct);

            if (responseJson == null)
            {
                // Notifications get 202 Accepted with no body.
                WriteEmpty(context.Response, (int)HttpStatusCode.Accepted);
                return;
            }

            byte[] payload = Encoding.UTF8.GetBytes(responseJson);

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = payload.Length;
            AddCommonHeaders(context.Response);

            await context.Response.OutputStream.WriteAsync(payload, 0, payload.Length, CancellationToken.None);
            context.Response.Close();
        }

        private void WriteEmpty(HttpListenerResponse response, int statusCode)
        {
            response.StatusCode = statusCode;
            response.ContentLength64 = 0;
            AddCommonHeaders(response);
            response.Close();
        }

        private void AddCommonHeaders(HttpListenerResponse response)
        {
            response.AddHeader("Mcp-Session-Id", sessionId);
            response.AddHeader("MCP-Protocol-Version", McpConstants.PROTOCOL_VERSION);
        }

        private void TryWriteInternalError(HttpListenerContext context)
        {
            try { WriteEmpty(context.Response, (int)HttpStatusCode.InternalServerError); }
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
    }
}
