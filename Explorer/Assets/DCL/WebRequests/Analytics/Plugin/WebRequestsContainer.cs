using CDPBridges;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Prefs;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics.Metrics;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.Dumper;
using DCL.WebRequests.RequestsHub;
using System;
using Utility.Multithreading;
using Utility.Storage;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsContainer : IDisposable
    {
        public IWebRequestController WebRequestController { get; }

        public IWebRequestController SceneWebRequestController { get; }

        public WebRequestsAnalyticsContainer AnalyticsContainer { get; }

        public ChromeDevtoolProtocolClient ChromeDevtoolProtocolClient { get; }

        private readonly DebugWidgetBuilder? widget;

        private WebRequestsContainer(
            IWebRequestController webRequestController,
            IWebRequestController sceneWebRequestController,
            WebRequestsAnalyticsContainer analyticsContainer,
            ChromeDevtoolProtocolClient chromeDevtoolProtocolClient,
            DebugWidgetBuilder? widget)
        {
            WebRequestController = webRequestController;
            AnalyticsContainer = analyticsContainer;
            ChromeDevtoolProtocolClient = chromeDevtoolProtocolClient;
            this.widget = widget;
            SceneWebRequestController = sceneWebRequestController;
        }

        public WebRequestsPlugin CreatePlugin(bool isLocalSceneDevelopment)
        {
            AnalyticsContainer
               .BuildUpDebugWidget(isLocalSceneDevelopment)
               .AddTrackedMetric<ActiveCounter>()
               .AddTrackedMetric<Total>()
               .AddTrackedMetric<TotalFailed>()
               .AddTrackedMetric<BandwidthDown>()
               .AddTrackedMetric<BandwidthUp>()
               .AddTrackedMetric<ServeTimeSmallFileAverage>()
               .AddTrackedMetric<ServeTimePerMBAverage>()
               .AddTrackedMetric<FillRateAverage>()
               .AddTrackedMetric<TimeToFirstByteAverage>()
               .Build();

            widget?.AddSingleButton("Open Chrome DevTools", () =>
            {
                BridgeStartResult result = ChromeDevtoolProtocolClient.StartAndOpen();
                string? errorMessage = ErrorMessageFromBridgeResult(result);

                if (errorMessage != null)
                    NotificationsBusController
                       .Instance
                       .AddNotification(new ServerErrorNotification(errorMessage));
            });

            return new WebRequestsPlugin(AnalyticsContainer);
        }

        // ReSharper disable once ReturnTypeCanBeNotNullable
        private static string? ErrorMessageFromBridgeResult(BridgeStartResult result)
        {
            string message = result.Match(
                onSuccess: static () => null!,
                onBridgeStartError: static e => e.Match(
                    onWebSocketError: static e => $"Cannot start WebSocket server: {e.Exception.Message}",
                    onBrowserOpenError: static e => e.Match(
                        onErrorChromeNotInstalled: static () => "Chrome not installed",
                        onException: static e => $"Cannot open DevTools: {e.Message}")
                )
            );

            return message;
        }

        public static WebRequestsContainer Create(
            IWeb3IdentityCache web3IdentityProvider,
            IDebugContainerBuilder debugContainerBuilder,
            IDecentralandUrlsSource urlsSource,
            ChromeDevtoolProtocolClient chromeDevtoolProtocolClient,
            int coreBudget,
            int sceneBudget
        )
        {
            var options = new ArtificialDelayOptions.ElementBindingOptions();

            DebugWidgetBuilder? widget = debugContainerBuilder.TryAddWidget(IDebugContainerBuilder.Categories.WEB_REQUESTS);

            var analyticsContainer = new WebRequestsAnalyticsContainer(widget);

            WebRequestsDumper.Instance.AnalyticsContainer = analyticsContainer;

            var requestCompleteDebugMetric = new ElementBinding<ulong>(0);

            var cannotConnectToHostExceptionDebugMetric = new ElementBinding<ulong>(0);

            var sceneAvailableBudget = new ElementBinding<ulong>((ulong)sceneBudget);
            var coreAvailableBudget = new ElementBinding<ulong>((ulong)coreBudget);

            var requestHub = new RequestHub(urlsSource);

            IWebRequestController coreWebRequestController = new WebRequestController(analyticsContainer, web3IdentityProvider, requestHub, chromeDevtoolProtocolClient, new WebRequestBudget(coreBudget, coreAvailableBudget))
                                                            .WithDump(analyticsContainer)
                                                            .WithDebugMetrics(cannotConnectToHostExceptionDebugMetric, requestCompleteDebugMetric)
                                                            .WithArtificialDelay(options);

            IWebRequestController sceneWebRequestController = new WebRequestController(analyticsContainer, web3IdentityProvider, requestHub, chromeDevtoolProtocolClient, new WebRequestBudget(sceneBudget, sceneAvailableBudget))
                                                             .WithDump(analyticsContainer)
                                                             .WithDebugMetrics(cannotConnectToHostExceptionDebugMetric, requestCompleteDebugMetric)
                                                             .WithArtificialDelay(options);

            CreateStressTestUtility();
            CreateWebRequestDelayUtility();
            CreateWebRequestsMetricsDebugUtility();

            return new WebRequestsContainer(coreWebRequestController, sceneWebRequestController, analyticsContainer, chromeDevtoolProtocolClient, widget);

            void CreateWebRequestsMetricsDebugUtility()
            {
                debugContainerBuilder.TryAddWidget(IDebugContainerBuilder.Categories.WEB_REQUESTS_DEBUG_METRICS)
                                    ?.AddMarker("Requests cannot connect", cannotConnectToHostExceptionDebugMetric,
                                          DebugLongMarkerDef.Unit.NoFormat)
                                     .AddMarker("Requests complete", requestCompleteDebugMetric,
                                          DebugLongMarkerDef.Unit.NoFormat)
                                     .AddMarker("Core budget", coreAvailableBudget,
                                          DebugLongMarkerDef.Unit.NoFormat)
                                     .AddMarker("Scene budget", sceneAvailableBudget,
                                          DebugLongMarkerDef.Unit.NoFormat);
            }

            void CreateWebRequestDelayUtility()
            {
                debugContainerBuilder
                   .TryAddWidget(IDebugContainerBuilder.Categories.WEB_REQUESTS_DELAY)
                  ?.AddControlWithLabel(
                        "Use Artificial Delay",
                        new DebugToggleDef(options.Enable)
                    )
                   .AddControlWithLabel(
                        "Artificial Delay Seconds",
                        new DebugFloatFieldDef(options.Delay)
                    );
            }

            void CreateStressTestUtility()
            {
                var stressTestUtility = new WebRequestStressTestUtility(coreWebRequestController);

                var count = new ElementBinding<int>(50);
                var retriesCount = new ElementBinding<int>(3);
                var delayBetweenRequests = new ElementBinding<float>(0);

                debugContainerBuilder.TryAddWidget(IDebugContainerBuilder.Categories.WEB_REQUESTS_STRESS_TEST)
                                    ?.AddControlWithLabel("Count:", new DebugIntFieldDef(count))
                                     .AddControlWithLabel("Retries:", new DebugIntFieldDef(retriesCount))
                                     .AddControlWithLabel("Delay between requests (s):", new DebugFloatFieldDef(delayBetweenRequests))
                                     .AddControl(
                                          new DebugButtonDef("Start Success",
                                              () =>
                                              {
                                                  stressTestUtility.StartConcurrentAsync(count.Value, retriesCount.Value, false,
                                                                        delayBetweenRequests.Value)
                                                                   .Forget();
                                              }),
                                          new DebugButtonDef("Start Failure",
                                              () =>
                                              {
                                                  stressTestUtility.StartConcurrentAsync(count.Value, retriesCount.Value, true,
                                                                        delayBetweenRequests.Value)
                                                                   .Forget();
                                              }),
                                          new DebugHintDef("Concurrent"))
                                     .AddControl(
                                          new DebugButtonDef("Start Success",
                                              () =>
                                              {
                                                  stressTestUtility.StartSequentialAsync(count.Value, retriesCount.Value, false,
                                                                        delayBetweenRequests.Value)
                                                                   .Forget();
                                              }),
                                          new DebugButtonDef("Start Failure",
                                              () =>
                                              {
                                                  stressTestUtility.StartSequentialAsync(count.Value, retriesCount.Value, true,
                                                                        delayBetweenRequests.Value)
                                                                   .Forget();
                                              }),
                                          new DebugHintDef("Sequential"));
            }
        }

        public void SetKTXEnabled(bool enabled)
        {
            // TODO: Temporary until we rewrite FF to be static
            WebRequestController.RequestHub.SetKTXEnabled(enabled);
            SceneWebRequestController.RequestHub.SetKTXEnabled(enabled);
        }

        public void Dispose() =>
            WebRequestsDumper.Instance.AnalyticsContainer = null;
    }
}
