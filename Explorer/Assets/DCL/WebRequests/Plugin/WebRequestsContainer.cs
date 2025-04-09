using Best.HTTP.Caching;
using Best.HTTP.Shared;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.PluginSystem;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.HTTP2;
using DCL.WebRequests.RequestsHub;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.WebRequests
{
    public class WebRequestsContainer : DCLGlobalContainer<WebRequestsContainer.Settings>
    {
        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public WebRequestsMode WebRequestsMode { get; private set; } = WebRequestsMode.HTTP2;
            [field: SerializeField] public int CoreWebRequestsBudget { get; private set; } = 15;
            [field: SerializeField] public int SceneWebRequestsBudget { get; private set; } = 5;
            [field: SerializeField] public ushort CacheSizeGB { get; private set; } = 2; // 2 GB by default
            [field: SerializeField] public short PartialChunkSizeMB { get; private set; } = 2; // 2 MB by default
        }

        public WebRequestsMode WebRequestsMode => settings.WebRequestsMode;

        public IWebRequestController WebRequestController { get; private set; } = null!;

        public IWebRequestController SceneWebRequestController { get; private set; } = null!;

        public WebRequestsAnalyticsContainer AnalyticsContainer { get; private set; } = null!;

        public static async UniTask<WebRequestsContainer> Create(
            IPluginSettingsContainer settingsContainer,
            IWeb3IdentityCache web3IdentityProvider,
            ITexturesFuse texturesFuse,
            IDebugContainerBuilder debugContainerBuilder,
            bool isTextureCompressionEnabled,
            CancellationToken ct
        )
        {
            var container = new WebRequestsContainer();
            await settingsContainer.InitializePluginAsync(container, ct);

            ulong cacheSize = container.settings.CacheSizeGB * 1024UL * 1024UL * 1024UL;

            // initialize 2 gb cache that will be used for all HTTP2 requests including the special logic for partial ones
            var httpCache = new HTTPCache(new HTTPCacheOptions(TimeSpan.MaxValue, cacheSize));
            HTTPManager.LocalCache = httpCache;

            var options = new ArtificialDelayOptions.ElementBindingOptions();

            var analyticsContainer = WebRequestsAnalyticsContainer.Create(debugContainerBuilder.TryAddWidget("Web Requests"));

            var requestCompleteDebugMetric = new ElementBinding<ulong>(0);

            int coreBudget = container.settings.CoreWebRequestsBudget;
            int sceneBudget = container.settings.SceneWebRequestsBudget;

            var cannotConnectToHostExceptionDebugMetric = new ElementBinding<ulong>(0);
            var coreAvailableBudget = new ElementBinding<ulong>((ulong)coreBudget);
            var sceneAvailableBudget = new ElementBinding<ulong>((ulong)sceneBudget);

            var textureFuseRequestHub = new RequestHub(texturesFuse, httpCache, isTextureCompressionEnabled, container.WebRequestsMode);

            int partialChunkSize = container.settings.PartialChunkSizeMB * 1024 * 1024;

            IWebRequestController baseWebRequestController = new RedirectWebRequestController(container.WebRequestsMode,
                                                                 new DefaultWebRequestController(analyticsContainer, web3IdentityProvider, textureFuseRequestHub),
                                                                 new Http2WebRequestController(analyticsContainer, web3IdentityProvider, textureFuseRequestHub, httpCache, partialChunkSize),
                                                                 textureFuseRequestHub)
                                                            .WithLog()
                                                            .WithArtificialDelay(options);

            IWebRequestController coreWebRequestController = baseWebRequestController.WithBudget(coreBudget, coreAvailableBudget);
            IWebRequestController sceneWebRequestController = baseWebRequestController.WithBudget(sceneBudget, sceneAvailableBudget);

            CreateStressTestUtility();
            CreateWebRequestDelayUtility();
            CreateWebRequestsMetricsDebugUtility();

            container.AnalyticsContainer = analyticsContainer;
            container.WebRequestController = baseWebRequestController;
            container.SceneWebRequestController = sceneWebRequestController;
            return container;

            void CreateWebRequestsMetricsDebugUtility()
            {
                debugContainerBuilder
                   .TryAddWidget("Web Requests Debug Metrics")
 ?
.AddMarker("Requests cannot connect", cannotConnectToHostExceptionDebugMetric,
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
                   .TryAddWidget("Web Requests Delay")
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

                debugContainerBuilder.TryAddWidget("Web Requests Stress Tress")
               ?
              .AddControlWithLabel("Count:", new DebugIntFieldDef(count))
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
    }
}
