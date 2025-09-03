using CDPBridges;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.WebRequests.ChromeDevtool
{
    public class ChromeDevtoolProtocolClient : IDisposable
    {
        /// <summary>
        /// https://chromedevtools.github.io/devtools-protocol/1-3/Network/#event-requestWillBeSent
        /// Loader identifier. Empty string if the request is fetched from worker.
        /// </summary>
        public const string LOADER_ID = "";
        private const string DOCUMENT_URL = "https://local.client.decentraland.com/";

        private readonly IBridge bridge;
        private readonly CancellationTokenSource cancellationTokenSource;
        private int atomicRequestIdIncrement;

        private ChromeDevtoolProtocolClient(IBridge bridge)
        {
            this.bridge = bridge;
            cancellationTokenSource = new CancellationTokenSource();
            atomicRequestIdIncrement = 1;
        }

        public static ChromeDevtoolProtocolClient New(bool startOnCreation)
        {
            // TODO activation via debug panel, tracking status of activation
            Bridge bridge = new Bridge(
                browser: new NativeBrowser(),
                logger: new UnityLogger(ReportCategory.CHROME_DEVTOOL_PROTOCOL)
            );

            if (startOnCreation)
            {
                BridgeStartResult result = bridge.Start();

                if (result.IsBridgeStartError(out BridgeStartError? error))
                    ReportHub.LogError(
                        ReportCategory.CHROME_DEVTOOL_PROTOCOL,
                        $"Cannot start bridge on creation: {error!.Value}"
                    );
            }

            return new ChromeDevtoolProtocolClient(bridge);
        }

        /// <summary>
        /// CAUTION headers must not be repooled until the scope is no longer needed
        /// </summary>
        public NotifyWebRequestScope NotifyWebRequestStart(string url, string method, Dictionary<string, string> headers)
        {
            int id = Interlocked.Add(ref atomicRequestIdIncrement, 1);
            HttpMethod httpMethod = FromString(method);
            Request request = new Request(url, httpMethod, headers, ReferrerPolicy.Origin());

            CDPEvent.Network_requestWillBeSent network = new CDPEvent.Network_requestWillBeSent(
                id,
                LOADER_ID,
                DOCUMENT_URL,
                request,
                MonotonicTime.Now,
                TimeSinceEpoch.Now,
                new Initiator(Initiator.Type.other)
            );

            CDPEvent cdpEvent = CDPEvent.FromNetwork_requestWillBeSent(network);
            bridge.SendEventAsync(cdpEvent, cancellationTokenSource.Token).Forget();
            return new NotifyWebRequestScope(id, url, headers, bridge, cancellationTokenSource.Token);
        }

        public void Dispose()
        {
            bridge.Dispose();
        }

        private static HttpMethod FromString(string raw)
        {
            bool success = Enum.TryParse(raw, true, out HttpMethod result);

            if (success == false)
                ReportHub.LogError(ReportCategory.CHROME_DEVTOOL_PROTOCOL, $"Cannot parse http method: {raw}");

            return result;
        }
    }

    public readonly struct NotifyWebRequestScope
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

        public async UniTaskVoid NotifyFinishAsync(int status, Dictionary<string, string>? headers, string mimeType, int encodedDataLength)
        {
            // create a copy to don't mess up lifetimes and pooling
            using var _ = ThreadSafeDictionaryPool<string, string>.SHARED.Get(out Dictionary<string, string> headersCopy);

            if (headers != null)
                foreach ((string key, string value) in headers)
                    headersCopy.Add(key, value);

            TimeSinceEpoch epochTimestamp = TimeSinceEpoch.Now;

            NetworkResponse response = new NetworkResponse(
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

            CDPEvent.Network_responseReceived network = new CDPEvent.Network_responseReceived(id, ChromeDevtoolProtocolClient.LOADER_ID, MonotonicTime.Now, ResourceType.Fetch, response);
            CDPEvent cdp = CDPEvent.FromNetwork_responseReceived(network);
            await bridge.SendEventAsync(cdp, token);

            CDPEvent.Network_dataReceived dataReceived = new CDPEvent.Network_dataReceived(id, MonotonicTime.Now, encodedDataLength, encodedDataLength); // reason of duplication of encodedDataLength: Unity provides only downloadedBytes
            cdp = CDPEvent.FromNetwork_dataReceived(dataReceived);
            await bridge.SendEventAsync(cdp, token);

            CDPEvent.Network_loadingFinished finished = new CDPEvent.Network_loadingFinished(id, MonotonicTime.Now, encodedDataLength);
            cdp = CDPEvent.FromNetwork_loadingFinished(finished);
            await bridge.SendEventAsync(cdp, token);
        }

        public void NotifyFailed(string errorText, bool hasBeenCancelled)
        {
            CDPEvent.Network_loadingFailed failed = new CDPEvent.Network_loadingFailed(id, MonotonicTime.Now, ResourceType.Fetch, errorText, hasBeenCancelled);
            CDPEvent cdp = CDPEvent.FromNetwork_loadingFailed(failed);
            bridge.SendEventAsync(cdp, token).Forget();
        }
    }
}
