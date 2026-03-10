using CDPBridges;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.WebRequests.Analytics;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.WebRequests.ChromeDevtool
{
    public class ChromeDevToolHandler : IWebRequestAnalyticsHandler, IDisposable
    {
        /// <summary>
        ///     https://chromedevtools.github.io/devtools-protocol/1-3/Network/#event-requestWillBeSent
        ///     Loader identifier. Empty string if the request is fetched from worker.
        /// </summary>
        public const string LOADER_ID = "";

        private const string DOCUMENT_URL = "https://local.client.decentraland.com/";

        private const int INITIAL_CAPACITY = 1024;

        private readonly Dictionary<UnityWebRequest, ChromeDevToolWebRequestScope> uwrToScope;
        private readonly List<ChromeDevToolWebRequestScope?> webRequestScopes;

        private readonly IBridge bridge;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public ChromeDevToolHandler(int maxConcurrency, IBridge bridge)
        {
            // UWR gets disposed of on completion
            uwrToScope = new Dictionary<UnityWebRequest, ChromeDevToolWebRequestScope>(maxConcurrency);

            // The context has to be stored forever as it's possible to inspect the whole web requests history and invoke the corresponding callbacks
            webRequestScopes = new List<ChromeDevToolWebRequestScope>(INITIAL_CAPACITY);

            this.bridge = bridge;
        }

#if UNITY_INCLUDE_TESTS || UNITY_EDITOR
        public static ChromeDevToolHandler NewForTest() =>
            new (1, new Bridge());
#endif

        public static ChromeDevToolHandler New(bool startOnCreation, IAppArgs appArgs)
        {
            const int PORT = 1473;

            ChromeDevToolHandler newInstance = null!;

            var bridge = new Bridge(
                handleMethod: HandleCdpMethod,
                browser: new CreatorHubBrowser(appArgs, PORT),
                logger: new DCLLogger(ReportCategory.CHROME_DEVTOOL_PROTOCOL),
                port: PORT
            );

            newInstance = new ChromeDevToolHandler(100, bridge);

            if (startOnCreation)
                newInstance.StartAndOpen();

            return newInstance;

            CDPResult? HandleCdpMethod(int messageId, CDPMethod method)
            {
                if (method.GetKind() == CDPMethod.Kind.Unknown)
                    return null;

                CDPResult? result = null;

                if (method.IsGetResponseBody(out CDPMethod.GetResponseBody getResponseBody))
                {
                    lock (newInstance.webRequestScopes)
                    {
                        ChromeDevToolWebRequestScope? scope = newInstance.webRequestScopes.ElementAtOrDefault(getResponseBody.RequestId - 1);

                        if (scope != null)
                            result = getResponseBody.RespondWith(new CDPResult.GetResponseBody(scope.responseBody, false));
                    }
                }

                return result;
            }
        }

        public BridgeStatus Status => bridge.Status;

        public BridgeStartResult StartAndOpen()
        {
            BridgeStartResult result = bridge.Start();

            if (result.IsBridgeStartError(out BridgeStartError error))
                ReportHub.LogError(
                    ReportCategory.CHROME_DEVTOOL_PROTOCOL,
                    $"Cannot start bridge on creation: {error!}"
                );

            return result;
        }

        private bool isEnabled => Status == BridgeStatus.HasListeners;

        public void OnBeforeBudgeting<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request) where T: struct, ITypedWebRequest where TWebRequestArgs: struct
        {
            if (!isEnabled) return;

            // Reserve an index in the list
            int index;

            lock (webRequestScopes)
            {
                index = webRequestScopes.Count;
                webRequestScopes.Add(null);
            }

            PoolExtensions.Scope<Dictionary<string, string>> pooledHeaders = envelope.Headers(out Dictionary<string, string> headers);

            int id = index + 1;
            HttpMethod httpMethod = FromString(request.UnityWebRequest.method);
            var cdpRequest = new Request(envelope.CommonArguments.URL, httpMethod, headers, ReferrerPolicy.Origin(), envelope.GetPostData());

            //  Queueing = response.timing.requestTime - requestWillBeSent.timestamp
            var network = new CDPEvent.Network_requestWillBeSent(
                id,
                LOADER_ID,
                DOCUMENT_URL,
                cdpRequest,
                MonotonicTime.Now,
                TimeSinceEpoch.Now,
                new Initiator(Initiator.Type.other)
            );

            var cdpEvent = CDPEvent.FromNetwork_requestWillBeSent(network);
            bridge.SendEventAsync(cdpEvent, cancellationTokenSource.Token).Forget();

            var scope = new ChromeDevToolWebRequestScope(id, envelope.CommonArguments.URL, pooledHeaders, bridge, cancellationTokenSource.Token);

            lock (webRequestScopes) { webRequestScopes[index] = scope; }

            lock (uwrToScope) { uwrToScope[request.UnityWebRequest] = scope; }
        }

        private bool TryGetScope(UnityWebRequest uwr, out ChromeDevToolWebRequestScope scope)
        {
            lock (uwrToScope) { return uwrToScope.TryGetValue(uwr, out scope); }
        }

        private bool RemoveUwr(UnityWebRequest uwr, out ChromeDevToolWebRequestScope scope)
        {
            lock (uwrToScope) { return uwrToScope.Remove(uwr, out scope); }
        }

        public void Update(float dt)
        {
            if (!isEnabled) return;

            lock (uwrToScope)
            {
                foreach ((UnityWebRequest webRequest, ChromeDevToolWebRequestScope scope) in uwrToScope)
                    scope.Update(webRequest);
            }
        }

        public void OnRequestStarted<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request, DateTime startedAt) where T: struct, ITypedWebRequest where TWebRequestArgs: struct
        {
            if (!isEnabled) return;

            if (TryGetScope(request.UnityWebRequest, out ChromeDevToolWebRequestScope scope))
                scope.OnRequestStarted();
        }

        public void OnRequestFinished<T>(T request, TimeSpan duration) where T: ITypedWebRequest
        {
            if (!isEnabled) return;

            UnityWebRequest uwr = request.UnityWebRequest;

            if (RemoveUwr(uwr, out ChromeDevToolWebRequestScope scope))
            {
                var statusCode = (int)uwr.responseCode;

                Dictionary<string, string> responseHeaders = uwr.GetResponseHeaders();

                string mimeType = uwr.GetRequestHeader("Content-Type") ?? "application/octet-stream";
                var encodedDataLength = (int)uwr.downloadedBytes;

                scope.NotifyFinishAsync(uwr.url, statusCode, responseHeaders, mimeType, encodedDataLength, uwr.downloadHandler).Forget();
            }
        }

        public void OnProcessDataFinished<T>(T request) where T: ITypedWebRequest
        {
            // Chrome DevTools doesn't expose data processing
        }

        public void OnException<T>(T request, Exception exception, TimeSpan duration) where T: ITypedWebRequest
        {
            if (!isEnabled) return;

            if (RemoveUwr(request.UnityWebRequest, out ChromeDevToolWebRequestScope scope))
            {
                bool hasBeenCancelled = exception is OperationCanceledException or AggregateException { InnerException: OperationCanceledException };

                scope.NotifyFailed(hasBeenCancelled ? "Cancelled" : $"Engine exception: {exception.Message}", hasBeenCancelled);
            }
        }

        public void OnException<T>(T request, UnityWebRequestException exception, TimeSpan duration) where T: ITypedWebRequest
        {
            if (!isEnabled) return;

            if (RemoveUwr(request.UnityWebRequest, out ChromeDevToolWebRequestScope scope))
                scope.NotifyFailed(exception.Error, false);
        }

        public void Dispose()
        {
            bridge.Dispose();
        }

        private static HttpMethod FromString(string raw)
        {
            bool success = Enum.TryParse(raw, true, out HttpMethod result);

            if (!success)
                ReportHub.LogError(ReportCategory.CHROME_DEVTOOL_PROTOCOL, $"Cannot parse http method: {raw}");

            return result;
        }
    }
}
