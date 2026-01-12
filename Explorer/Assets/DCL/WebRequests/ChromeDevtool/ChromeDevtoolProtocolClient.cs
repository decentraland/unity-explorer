using CDPBridges;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private const int PORT = 1473;

        private readonly IBridge bridge;
        private readonly CancellationTokenSource cancellationTokenSource;

        private readonly List<NotifyWebRequestScope?> webRequestScopes = new (500);

        public BridgeStatus Status => bridge.Status;

        private ChromeDevtoolProtocolClient(IBridge bridge)
        {
            this.bridge = bridge;
            cancellationTokenSource = new CancellationTokenSource();

            bridge.HandleMethod = HandleCdpMethod;
        }

#if UNITY_INCLUDE_TESTS || UNITY_EDITOR
        public static ChromeDevtoolProtocolClient NewForTest() =>
            New(false, new ApplicationParametersParser());
#endif

        public static ChromeDevtoolProtocolClient New(bool startOnCreation, IAppArgs appArgs)
        {
            Bridge bridge = new Bridge(
                browser: new CreatorHubBrowser(appArgs, PORT),
                logger: new DCLLogger(ReportCategory.CHROME_DEVTOOL_PROTOCOL),
                port: PORT
            );

            ChromeDevtoolProtocolClient newInstance = new ChromeDevtoolProtocolClient(bridge);

            if (startOnCreation)
                newInstance.StartAndOpen();

            return newInstance;
        }

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

        /// <summary>
        /// CAUTION headers must not be repooled until the scope is no longer needed
        /// </summary>
        public NotifyWebRequestScope NotifyWebRequestStart(string url, string method, Dictionary<string, string> headers, string? postData)
        {
            // Reserve an index in the list
            int index;

            lock (webRequestScopes)
            {
                index = webRequestScopes.Count;
                webRequestScopes.Add(null);
            }

            int id = index + 1;
            HttpMethod httpMethod = FromString(method);
            var request = new Request(url, httpMethod, headers, ReferrerPolicy.Origin(), postData);

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

            var scope = new NotifyWebRequestScope(id, url, headers, bridge, cancellationTokenSource.Token);

            lock (webRequestScopes) { webRequestScopes[index] = scope; }

            return scope;
        }

        private CDPResult? HandleCdpMethod(int messageId, CDPMethod method)
        {
            if (method.GetKind() == CDPMethod.Kind.Unknown)
                return null;

            CDPResult? result = null;

            if (method.IsGetResponseBody(out CDPMethod.GetResponseBody getResponseBody))
            {
                lock (webRequestScopes)
                {
                    NotifyWebRequestScope? scope = webRequestScopes.ElementAtOrDefault(getResponseBody.RequestId - 1);

                    if (scope != null)
                        result = getResponseBody.RespondWith(new CDPResult.GetResponseBody(scope.responseBody, false));
                }
            }

            return result;
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
}
