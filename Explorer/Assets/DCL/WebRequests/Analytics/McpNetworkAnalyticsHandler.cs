using Cysharp.Threading.Tasks;
using System;
using UnityEngine.Networking;

namespace DCL.WebRequests.Analytics
{
    /// <summary>
    ///     A lightweight <see cref="IWebRequestAnalyticsHandler" /> that records every finished request into an
    ///     <see cref="McpNetworkLogBuffer" /> for the MCP <c>get_network_log</c> tool. Taps the same seam as the
    ///     Chrome DevTools <c>ChromeDevToolHandler</c> (see <c>docs/mcp-automation.md</c> for coverage details), but
    ///     unlike it keeps no per-request scope and streams nothing: it captures the final shape of a request once,
    ///     at completion or failure. Dormant until <see cref="StartRecording" /> supplies a buffer.
    /// </summary>
    public sealed class McpNetworkAnalyticsHandler : IWebRequestAnalyticsHandler
    {
        private const string UNKNOWN_MIME = "application/octet-stream";

        private McpNetworkLogBuffer? buffer;

        /// <summary>
        ///     Starts recording into <paramref name="target" />. Every callback returns before touching the request
        ///     until then, so a client whose MCP server never starts pays no lock, no timestamp and none of the three
        ///     string allocations that reading a <c>UnityWebRequest</c> back costs on every finished request.
        /// </summary>
        public void StartRecording(McpNetworkLogBuffer target)
        {
            buffer = target;
        }

        public void Update(float dt) { }

        public void OnBeforeBudgeting<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request) where T: struct, ITypedWebRequest where TWebRequestArgs: struct { }

        public void OnRequestStarted<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request, DateTime startedAt) where T: struct, ITypedWebRequest where TWebRequestArgs: struct { }

        public void OnProcessDataFinished<T>(T request) where T: ITypedWebRequest { }

        public void OnRequestFinished<T>(T request, TimeSpan duration) where T: ITypedWebRequest
        {
            if (buffer == null) return;

            UnityWebRequest uwr = request.UnityWebRequest;
            string mimeType = uwr.GetResponseHeader("Content-Type") ?? UNKNOWN_MIME;

            buffer.Append(uwr.url, uwr.method, (int)uwr.responseCode, mimeType, (long)uwr.downloadedBytes, duration.TotalMilliseconds, failed: false, failureReason: null);
        }

        public void OnException<T>(T request, Exception exception, TimeSpan duration) where T: ITypedWebRequest
        {
            if (buffer == null) return;

            bool cancelled = exception is OperationCanceledException or AggregateException { InnerException: OperationCanceledException };
            Record(buffer, request.UnityWebRequest, duration, cancelled ? "Cancelled" : $"Engine exception: {exception.Message}");
        }

        public void OnException<T>(T request, UnityWebRequestException exception, TimeSpan duration) where T: ITypedWebRequest
        {
            if (buffer == null) return;

            Record(buffer, request.UnityWebRequest, duration, exception.Error);
        }

        private static void Record(McpNetworkLogBuffer target, UnityWebRequest uwr, TimeSpan duration, string failureReason)
        {
            string mimeType = uwr.GetResponseHeader("Content-Type") ?? UNKNOWN_MIME;
            target.Append(uwr.url, uwr.method, (int)uwr.responseCode, mimeType, (long)uwr.downloadedBytes, duration.TotalMilliseconds, failed: true, failureReason);
        }
    }
}
