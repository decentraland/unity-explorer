using CDPBridges;
using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Networking;
using UnityEngine.Pool;

namespace DCL.WebRequests.ChromeDevtool
{
    public class ChromeDevToolWebRequestScope
    {
        private const string DEFAULT_CHARSET = "utf-8";
        private const bool DEFAULT_CONNECTION_REUSED = true; // consider connection as reused by default
        private const int DEFAULT_CONNECTION_ID = 1;
        private const string SUCCESS_STATUS_TEXT = "OK"; // Unity doesn't provide status texts
        private const string DEFAULT_CACHE_STORAGE_CACHE_NAME = "DEFAULT";
        private const string DEFAULT_PROTOCOL = "HTTP/v1.1"; // Unity doesn't expose protocol

        private readonly int id;
        private readonly string url;
        private readonly PoolExtensions.Scope<Dictionary<string, string>> requestHeaders;
        private readonly IBridge bridge;
        private readonly CancellationToken token;

        // Is not set if the request has not been actually started
        private ResourceTiming? resourceTiming;

        public ChromeDevToolWebRequestScope(int id, string url, PoolExtensions.Scope<Dictionary<string, string>> requestHeaders, IBridge bridge, CancellationToken token)
        {
            this.id = id;
            this.url = url;
            this.requestHeaders = requestHeaders;
            this.bridge = bridge;
            this.token = token;
        }

        internal string responseBody { get; private set; } = string.Empty;

        public void OnRequestStarted()
        {
            resourceTiming = ResourceTiming.CreateFromUnityWebRequestStarted(MonotonicTime.Now.Seconds);
        }

        public async UniTaskVoid NotifyFinishAsync(string effectiveUrl, int status, Dictionary<string, string> responseHeaders, string mimeType, int encodedDataLength,
            DownloadHandler? downloadHandler)
        {
            using PoolExtensions.Scope<Dictionary<string, string>> _ = requestHeaders;

            responseBody = GetResponseBody(downloadHandler);

            TimeSinceEpoch epochTimestamp = TimeSinceEpoch.Now;

            var response = new NetworkResponse(
                effectiveUrl,
                status,
                SUCCESS_STATUS_TEXT,
                responseHeaders,
                mimeType,
                DEFAULT_CHARSET,
                requestHeaders.Value,
                DEFAULT_CONNECTION_REUSED,
                DEFAULT_CONNECTION_ID,
                encodedDataLength,
                resourceTiming,
                epochTimestamp,
                DEFAULT_CACHE_STORAGE_CACHE_NAME,
                DEFAULT_PROTOCOL,
                SecurityState.Secure()
            );

            var network = new CDPEvent.Network_responseReceived(id, ChromeDevToolHandler.LOADER_ID, MonotonicTime.Now, ResourceType.Fetch, response);
            var cdp = CDPEvent.FromNetwork_responseReceived(network);
            await bridge.SendEventAsync(cdp, token);

            var dataReceived = new CDPEvent.Network_dataReceived(id, MonotonicTime.Now, encodedDataLength, encodedDataLength); // reason of duplication of encodedDataLength: Unity provides only downloadedBytes
            cdp = CDPEvent.FromNetwork_dataReceived(dataReceived);
            await bridge.SendEventAsync(cdp, token);

            var finished = new CDPEvent.Network_loadingFinished(id, MonotonicTime.Now, encodedDataLength);
            cdp = CDPEvent.FromNetwork_loadingFinished(finished);
            await bridge.SendEventAsync(cdp, token);
        }

        public void NotifyFailed(string errorText, bool hasBeenCancelled)
        {
            using PoolExtensions.Scope<Dictionary<string, string>> _ = requestHeaders;

            var failed = new CDPEvent.Network_loadingFailed(id, MonotonicTime.Now, ResourceType.Fetch, errorText, hasBeenCancelled);
            var cdp = CDPEvent.FromNetwork_loadingFailed(failed);
            bridge.SendEventAsync(cdp, token).Forget();
        }

        public void Update(UnityWebRequest uwr)
        {
            if (resourceTiming == null) return;

            ResourceTiming rtValue = resourceTiming.Value;

            // For UWR these values are very rough, and can't be calculated directly

            // 1. sendEnd - when uploadProgress hits 1.0
            UploadHandler? uploadHandler = uwr.uploadHandler;

            if (uploadHandler == null || uploadHandler.progress >= 1)
                rtValue.AssignSendEnd(MonotonicTime.Now.Seconds);

            // 2. receiveHeadersEnd - when downloadedBytes > 0
            DownloadHandler? downloadHandler = uwr.downloadHandler;

            if (downloadHandler == null || uwr.downloadedBytes > 0)
                rtValue.AssignReceiveHeadersEnd(MonotonicTime.Now.Seconds);

            resourceTiming = rtValue;
        }

        private static string GetResponseBody(DownloadHandler? downloadHandler) =>
            downloadHandler switch
            {
                DownloadHandlerBuffer or DownloadHandlerScript => downloadHandler.text,
                _ => string.Empty,
            };
    }
}
