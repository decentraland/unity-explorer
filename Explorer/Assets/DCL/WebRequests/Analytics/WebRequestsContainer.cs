using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics.Metrics;
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

        public static WebRequestsContainer Create(IWeb3IdentityCache web3IdentityProvider, IDebugContainerBuilder debugContainerBuilder)
        {
            var analyticsContainer = new WebRequestsAnalyticsContainer()
                                    .AddTrackedMetric<ActiveCounter>()
                                    .AddTrackedMetric<Total>()
                                    .AddTrackedMetric<TotalFailed>()
                                    .AddTrackedMetric<BandwidthDown>()
                                    .AddTrackedMetric<BandwidthUp>();

            var options = new ElementBindingOptions();

            debugContainerBuilder
               .AddWidget("Web Requests Delay")
               .AddControlWithLabel(
                    "Use Artificial Delay",
                    new DebugToggleDef(options.Enable)
                )
               .AddControlWithLabel(
                    "Artificial Delay Seconds",
                    new DebugFloatFieldDef(options.Delay)
                );

            var webRequestController = new WebRequestController(analyticsContainer, web3IdentityProvider)
                                      .WithLog()
                                      .WithArtificialDelay(options)
                                      .WithSafeMainThread();

            return new WebRequestsContainer(webRequestController, analyticsContainer);
        }

        private class ElementBindingOptions : ArtificialDelayWebRequestController.IReadOnlyOptions
        {
            public float ArtificialDelaySeconds => Delay.Value;
            public bool UseDelay => Enable.Value;

            public readonly IElementBinding<bool> Enable;
            public readonly IElementBinding<float> Delay;

            public ElementBindingOptions() : this(
                new PersistentElementBinding<bool>(PersistentSetting.CreateBool("webRequestsArtificialDelayEnable", false).WithCached()),
                new PersistentElementBinding<float>(PersistentSetting.CreateFloat("webRequestsArtificialDelaySeconds", 10).WithCached())
            ) { }

            public ElementBindingOptions(IElementBinding<bool> enable, IElementBinding<float> delay)
            {
                this.Enable = enable;
                this.Delay = delay;
            }
        }
    }
}
