using CDPBridges;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.PluginSystem;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics.Metrics;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.Dumper;
using DCL.WebRequests.RequestsHub;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsContainer : DCLGlobalContainer<WebRequestsContainer.Settings>
    {
        private DebugWidgetBuilder? widget;

        private DebugMetricsAnalyticsHandler debugMetricsAnalyticsHandler;

        public IWebRequestController WebRequestController { get; private set; }

        public IWebRequestController SceneWebRequestController { get; private set; }

        public WebRequestsAnalyticsContainer AnalyticsContainer { get; private set; }

        public ChromeDevToolHandler ChromeDevToolHandler { get; private set; }

        private WebRequestsContainer() { }

        public override void Dispose() =>
            WebRequestsDumper.Instance.AnalyticsHandler = null;

        public WebRequestsPlugin CreatePlugin(bool isLocalSceneDevelopment)
        {
            if (widget != null)
            {
                debugMetricsAnalyticsHandler.BuildUpDebugWidget(isLocalSceneDevelopment)
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

                widget.AddSingleButton("Open Chrome DevTools", () =>
                {
                    BridgeStartResult result = ChromeDevToolHandler.StartAndOpen();
                    string? errorMessage = ErrorMessageFromBridgeResult(result);

                    if (errorMessage != null)
                        NotificationsBusController
                           .Instance
                           .AddNotification(new ServerErrorNotification(errorMessage));
                });
            }

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

        public static async UniTask<WebRequestsContainer> CreateAsync(
            IPluginSettingsContainer pluginSettingsContainer,
            IWeb3IdentityCache web3IdentityProvider,
            IDebugContainerBuilder debugContainerBuilder,
            IDecentralandUrlsSource urlsSource,
            ChromeDevToolHandler chromeDevtoolProtocolHandler,
            SentrySampler? sentrySampler,
            CancellationToken ct
        )
        {
            var container = new WebRequestsContainer();

            await container.InitializeContainerAsync<WebRequestsContainer, Settings>(pluginSettingsContainer, ct, container =>
            {
                var options = new ArtificialDelayOptions.ElementBindingOptions();

                DebugWidgetBuilder? widget = debugContainerBuilder.TryAddWidget(IDebugContainerBuilder.Categories.WEB_REQUESTS);

                int coreBudget = container.settings.CoreWebRequestsBudget;
                int sceneBudget = container.settings.SceneWebRequestsBudget;

                SentryWebRequestHandler? sentryWebRequestHandler = null;

                if (sentrySampler != null)
                {
                    var sentryWebRequestSampler = new SentryWebRequestSampler(urlsSource, container.settings.UrlsToSample, coreBudget);
                    sentrySampler.Add(sentryWebRequestSampler);

                    sentryWebRequestHandler = new SentryWebRequestHandler(sentryWebRequestSampler);
                }

                var debugHandler = new DebugMetricsAnalyticsHandler(widget);
                container.debugMetricsAnalyticsHandler = debugHandler;
                var dumpHandler = new WebRequestDumpAnalyticsHandler();

                WebRequestsDumper.Instance.AnalyticsHandler = dumpHandler;

                var analyticsContainer = new WebRequestsAnalyticsContainer(sentryWebRequestHandler, dumpHandler, debugHandler, chromeDevtoolProtocolHandler);

                var requestCompleteDebugMetric = new ElementBinding<ulong>(0);

                var cannotConnectToHostExceptionDebugMetric = new ElementBinding<ulong>(0);

                var sceneAvailableBudget = new ElementBinding<ulong>((ulong)sceneBudget);
                var coreAvailableBudget = new ElementBinding<ulong>((ulong)coreBudget);

                var requestHub = new RequestHub(urlsSource);

                IWebRequestController coreWebRequestController = new WebRequestController(analyticsContainer, web3IdentityProvider, requestHub, new WebRequestBudget(coreBudget, coreAvailableBudget))
                                                                .WithDump(debugHandler, dumpHandler)
                                                                .WithDebugMetrics(cannotConnectToHostExceptionDebugMetric, requestCompleteDebugMetric)
                                                                .WithArtificialDelay(options);

                IWebRequestController sceneWebRequestController = new WebRequestController(analyticsContainer, web3IdentityProvider, requestHub, new WebRequestBudget(sceneBudget, sceneAvailableBudget))
                                                                 .WithDump(debugHandler, dumpHandler)
                                                                 .WithDebugMetrics(cannotConnectToHostExceptionDebugMetric, requestCompleteDebugMetric)
                                                                 .WithArtificialDelay(options);

                CreateStressTestUtility();
                CreateWebRequestDelayUtility();
                CreateWebRequestsMetricsDebugUtility();

                container.widget = widget;
                container.WebRequestController = coreWebRequestController;
                container.SceneWebRequestController = sceneWebRequestController;
                container.AnalyticsContainer = analyticsContainer;
                container.ChromeDevToolHandler = chromeDevtoolProtocolHandler;

                return UniTask.CompletedTask;

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
            });

            return container;
        }

        public void SetKTXEnabled(bool enabled)
        {
            // TODO: Temporary until we rewrite FF to be static
            WebRequestController.RequestHub.SetKTXEnabled(enabled);
            SceneWebRequestController.RequestHub.SetKTXEnabled(enabled);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public int CoreWebRequestsBudget { get; private set; } = 15;
            [field: SerializeField] public int SceneWebRequestsBudget { get; private set; } = 5;

            [field: SerializeField]
            public SentryWebRequestSampler.SentryTransactionConfiguration[] UrlsToSample { get; private set; } = Array.Empty<SentryWebRequestSampler.SentryTransactionConfiguration>();
        }
    }
}
