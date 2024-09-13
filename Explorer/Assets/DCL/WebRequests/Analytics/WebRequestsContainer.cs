using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics.Metrics;
using Utility.Multithreading;
using Utility.Storage;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsContainer
    {
        public IWebRequestController WebRequestController { get; }

        public IWebRequestsAnalyticsContainer AnalyticsContainer { get; }

        private WebRequestsContainer(IWebRequestController webRequestController, IWebRequestsAnalyticsContainer analyticsContainer)
        {
            WebRequestController = webRequestController;
            AnalyticsContainer = analyticsContainer;
        }

        public static WebRequestsContainer Create(IWeb3IdentityCache web3IdentityProvider,
            IDebugContainerBuilder debugContainerBuilder, int totalBudget, int perDomainBudget,
            MemoryBudget sharedDependenciesMemoryBudget)
        {
            WebRequestsAnalyticsContainer analyticsContainer = new WebRequestsAnalyticsContainer()
                                                              .AddTrackedMetric<ActiveCounter>()
                                                              .AddTrackedMetric<Total>()
                                                              .AddTrackedMetric<TotalFailed>()
                                                              .AddTrackedMetric<BandwidthDown>()
                                                              .AddTrackedMetric<BandwidthUp>();

            var options = new ElementBindingOptions();

            DebugWidgetBuilder? widgetBuilder = debugContainerBuilder
                                               .TryAddWidget("Web Requests Delay")
                                              ?.AddControlWithLabel(
                                                    "Use Artificial Delay",
                                                    new DebugToggleDef(options.Enable)
                                                )
                                               .AddControlWithLabel(
                                                    "Artificial Delay Seconds",
                                                    new DebugFloatFieldDef(options.Delay)
                                                );

            IWebRequestController webRequestController = new WebRequestController(analyticsContainer, web3IdentityProvider)
                                                        .WithLog()
                                                        .WithArtificialDelay(options)
                                                        .WithBudget(totalBudget, perDomainBudget,
                                                            sharedDependenciesMemoryBudget);

            widgetBuilder.AddMarker("Memory budget hold request", BudgetedWebRequestController.REQUESTS_HOLD_BY_BUDGET,
                    DebugLongMarkerDef.Unit.NoFormat)
                .AddMarker("Requests cannot connect", WebRequests.WebRequestController.REQUESTS_CANNOT_CONNECT,
                    DebugLongMarkerDef.Unit.NoFormat);
            
            CreateStressTestUtility(widgetBuilder);

            return new WebRequestsContainer(webRequestController, analyticsContainer);

            void CreateStressTestUtility(DebugWidgetBuilder? debugWidgetBuilder)
            {
                if (debugWidgetBuilder != null)
                {
                    var stressTestUtility = new WebRequestStressTestUtility(webRequestController);

                    var count = new ElementBinding<int>(50);
                    var retriesCount = new ElementBinding<int>(3);
                    var delayBetweenRequests = new ElementBinding<float>(0);

                    debugWidgetBuilder.AddControl(new DebugHintDef("Stress test"), null)
                                      .AddControlWithLabel("Count:", new DebugIntFieldDef(count))
                                      .AddControlWithLabel("Retries:", new DebugIntFieldDef(retriesCount))
                                      .AddControlWithLabel("Delay between requests (s):", new DebugFloatFieldDef(delayBetweenRequests))
                                      .AddControl(
                                           new DebugButtonDef("Start Success", () => { stressTestUtility.StartConcurrentAsync(count.Value, retriesCount.Value, false, delayBetweenRequests.Value).Forget(); }),
                                           new DebugButtonDef("Start Failure", () => { stressTestUtility.StartConcurrentAsync(count.Value, retriesCount.Value, true, delayBetweenRequests.Value).Forget(); }),
                                           new DebugHintDef("Concurrent"))
                                      .AddControl(
                                           new DebugButtonDef("Start Success", () => { stressTestUtility.StartSequentialAsync(count.Value, retriesCount.Value, false, delayBetweenRequests.Value).Forget(); }),
                                           new DebugButtonDef("Start Failure", () => { stressTestUtility.StartSequentialAsync(count.Value, retriesCount.Value, true, delayBetweenRequests.Value).Forget(); }),
                                           new DebugHintDef("Sequential"));
                }
            }
        }

        public class ElementBindingOptions : ArtificialDelayWebRequestController.IReadOnlyOptions
        {
            public readonly IElementBinding<bool> Enable;
            public readonly IElementBinding<float> Delay;
            private readonly PersistentSetting<bool> enableSetting;
            private readonly PersistentSetting<float> delaySetting;

            public ElementBindingOptions() : this(
                PersistentSetting.CreateBool("webRequestsArtificialDelayEnable", false),
                PersistentSetting.CreateFloat("webRequestsArtificialDelaySeconds", 10)
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
