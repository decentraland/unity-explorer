using CDPBridges;
using Cysharp.Threading.Tasks;
using DCL.Optimization.ThreadSafePool;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Networking;
using UnityEngine.Pool;

namespace DCL.WebRequests.ChromeDevtool
{
    public class NotifyWebRequestScope
    {
        private const string DEFAULT_CHARSET = "utf-8";
        private const bool DEFAULT_CONNECTION_REUSED = true; // consider connection as reused by default
        private const int DEFAULT_CONNECTION_ID = 1;
        private const string SUCCESS_STATUS_TEXT = "OK"; // Unity doesn't provide status texts
        private const string DEFAULT_CACHE_STORAGE_CACHE_NAME = "DEFAULT";
        private const string DEFAULT_PROTOCOL = "HTTP/v1.1"; // Unity doesn't expose protocol

        private readonly int id;
        private readonly string url;
        private readonly Dictionary<string, string> requestHeaders;
        private readonly IBridge bridge;
        private readonly CancellationToken token;

        public NotifyWebRequestScope(int id, string url, Dictionary<string, string> requestHeaders, IBridge bridge, CancellationToken token)
        {
            this.id = id;
            this.url = url;
            this.requestHeaders = requestHeaders;
            this.bridge = bridge;
            this.token = token;
        }

        internal string responseBody { get; private set; } = string.Empty;

        public async UniTaskVoid NotifyFinishAsync(int status, Dictionary<string, string>? headers, string mimeType, int encodedDataLength, DownloadHandler? downloadHandler)
        {
            // create a copy to don't mess up lifetimes and pooling
            using PooledObject<Dictionary<string, string>> _ = ThreadSafeDictionaryPool<string, string>.SHARED.Get(out Dictionary<string, string> headersCopy);

            responseBody = GetResponseBody(downloadHandler);

            if (headers != null)
                foreach ((string key, string value) in headers)
                    headersCopy.Add(key, value);

            TimeSinceEpoch epochTimestamp = TimeSinceEpoch.Now;

            var response = new NetworkResponse(
                url,
                status,
                SUCCESS_STATUS_TEXT,
                headersCopy,
                mimeType,
                DEFAULT_CHARSET,
                requestHeaders,
                DEFAULT_CONNECTION_REUSED,
                DEFAULT_CONNECTION_ID,
                encodedDataLength,
                epochTimestamp,
                DEFAULT_CACHE_STORAGE_CACHE_NAME,
                DEFAULT_PROTOCOL,
                SecurityState.Secure()
            );

            var network = new CDPEvent.Network_responseReceived(id, ChromeDevtoolProtocolClient.LOADER_ID, MonotonicTime.Now, ResourceType.Fetch, response);
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
            var failed = new CDPEvent.Network_loadingFailed(id, MonotonicTime.Now, ResourceType.Fetch, errorText, hasBeenCancelled);
            var cdp = CDPEvent.FromNetwork_loadingFailed(failed);
            bridge.SendEventAsync(cdp, token).Forget();
        }

        private static string GetResponseBody(DownloadHandler? downloadHandler) =>
            downloadHandler switch
            {
                DownloadHandlerBuffer or DownloadHandlerScript => downloadHandler.text,
                _ => string.Empty,
            };
    }
}
