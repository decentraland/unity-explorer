using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Prefs;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics.Metrics;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.RequestsHub;
using Utility.Multithreading;
using Utility.Storage;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsContainer
    {
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        public IWebRequestController WebRequestController { get; }

        public IWebRequestController SceneWebRequestController { get; }

        public IWebRequestsAnalyticsContainer AnalyticsContainer { get; }

        public ChromeDevtoolProtocolClient ChromeDevtoolProtocolClient { get; }

        public IDecentralandUrlsSource DecentralandUrlsSource { get; }

        private WebRequestsContainer(
            IWebRequestController webRequestController,
            IWebRequestController sceneWebRequestController,
            IWebRequestsAnalyticsContainer analyticsContainer,
            ChromeDevtoolProtocolClient chromeDevtoolProtocolClient,
            IDecentralandUrlsSource decentralandUrlsSource
        )
        {
            this.decentralandUrlsSource = decentralandUrlsSource;
            WebRequestController = webRequestController;
            AnalyticsContainer = analyticsContainer;
            ChromeDevtoolProtocolClient = chromeDevtoolProtocolClient;
            SceneWebRequestController = sceneWebRequestController;
        }

        public static WebRequestsContainer Create(
            IWeb3IdentityCache web3IdentityProvider,
            IDebugContainerBuilder debugContainerBuilder,
            IDecentralandUrlsSource urlsSource,
            ChromeDevtoolProtocolClient chromeDevtoolProtocolClient,
            IDecentralandUrlsSource decentralandUrlsSource,
            int coreBudget,
            int sceneBudget
        )
        {
            var options = new ElementBindingOptions();

            WebRequestsAnalyticsContainer analyticsContainer = new WebRequestsAnalyticsContainer()
                                                              .AddTrackedMetric<ActiveCounter>()
                                                              .AddTrackedMetric<Total>()
                                                              .AddTrackedMetric<TotalFailed>()
                                                              .AddTrackedMetric<BandwidthDown>()
                                                              .AddTrackedMetric<BandwidthUp>()
                                                              .AddTrackedMetric<ServerTimeSmallFileAverage>()
                                                              .AddTrackedMetric<ServeTimePerMBAverage>()
                                                              .AddTrackedMetric<FillRateAverage>()
                                                              .AddTrackedMetric<TimeToFirstByteAverage>();

            var requestCompleteDebugMetric = new ElementBinding<ulong>(0);

            var cannotConnectToHostExceptionDebugMetric = new ElementBinding<ulong>(0);
            var sceneAvailableBudget = new ElementBinding<ulong>((ulong)sceneBudget);
            var coreAvailableBudget = new ElementBinding<ulong>((ulong)coreBudget);

            var requestHub = new RequestHub(urlsSource);

            IWebRequestController coreWebRequestController = new WebRequestController(analyticsContainer, web3IdentityProvider, requestHub, chromeDevtoolProtocolClient)
                                                            .WithDebugMetrics(cannotConnectToHostExceptionDebugMetric, requestCompleteDebugMetric)
                                                            .WithLog()
                                                            .WithArtificialDelay(options)
                                                            .WithBudget(coreBudget, coreAvailableBudget);

            IWebRequestController sceneWebRequestController = new WebRequestController(analyticsContainer, web3IdentityProvider, requestHub, chromeDevtoolProtocolClient)
                                                             .WithDebugMetrics(cannotConnectToHostExceptionDebugMetric, requestCompleteDebugMetric)
                                                             .WithLog()
                                                             .WithArtificialDelay(options)
                                                             .WithBudget(sceneBudget, sceneAvailableBudget);

            CreateStressTestUtility();
            CreateWebRequestDelayUtility();
            CreateWebRequestsMetricsDebugUtility();

            return new WebRequestsContainer(coreWebRequestController, sceneWebRequestController, analyticsContainer, chromeDevtoolProtocolClient, decentralandUrlsSource);

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
            WebRequestController.requestHub.SetKTXEnabled(enabled);
            SceneWebRequestController.requestHub.SetKTXEnabled(enabled);
        }

        public class ElementBindingOptions : ArtificialDelayWebRequestController.IReadOnlyOptions
        {
            public readonly IElementBinding<bool> Enable;
            public readonly IElementBinding<float> Delay;
            private readonly PersistentSetting<bool> enableSetting;
            private readonly PersistentSetting<float> delaySetting;

            public ElementBindingOptions() : this(
                PersistentSetting.CreateBool(DCLPrefKeys.WEB_REQUEST_ARTIFICIAL_DELAY_ENABLED, false),
                PersistentSetting.CreateFloat(DCLPrefKeys.WEB_REQUEST_ARTIFICIAL_DELAY_SECONDS, 10)
            ) { }

            public ElementBindingOptions(PersistentSetting<bool> enableSetting, PersistentSetting<float> delaySetting)
            {
                this.enableSetting = enableSetting;
                this.delaySetting = delaySetting;
                Enable = new PersistentElementBinding<bool>(enableSetting);
                Delay = new PersistentElementBinding<float>(delaySetting);
            }

            public async UniTask<(float ArtificialDelaySeconds, bool UseDelay)> GetOptionsAsync()
            {
                await using (await ExecuteOnMainThreadScope.NewScopeWithReturnOnOriginalThreadAsync())
                    return (Delay.Value, Enable.Value);
            }

            public void ApplyValues(bool enable, float delay)
            {
                enableSetting.ForceSave(enable);
                delaySetting.ForceSave(delay);
            }
        }
    }
}
