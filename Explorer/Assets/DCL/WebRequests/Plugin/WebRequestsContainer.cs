using Best.HTTP.Caching;
using Best.HTTP.HostSetting;
using Best.HTTP.Proxies.Autodetect;
using Best.HTTP.Proxies;
using Best.HTTP.Shared;
using Best.HTTP.Shared.Logger;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PluginSystem;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.HTTP2;
using DCL.WebRequests.RequestsHub;
using Global.AppArgs;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.WebRequests
{
    public class WebRequestsContainer : DCLGlobalContainer<WebRequestsContainer.Settings>
    {
        private RequestHub requestHub = null!;

        public WebRequestsMode WebRequestsMode { get; private set; }

        public bool EnablePartialDownloading => WebRequestsMode != WebRequestsMode.UNITY && settings.Http2Settings.EnablePartialDownloading;

        public IWebRequestController WebRequestController { get; private set; } = null!;

        public IWebRequestController SceneWebRequestController { get; private set; } = null!;

        public WebRequestsAnalyticsContainer AnalyticsContainer { get; private set; } = null!;

        public static async UniTask<WebRequestsContainer> CreateAsync(
            IAppArgs appArgs,
            IPluginSettingsContainer settingsContainer,
            IWeb3IdentityCache web3IdentityProvider,
            IDecentralandUrlsSource urlsSource,
            IDebugContainerBuilder debugContainerBuilder,
            bool ktxEnabled,
            CancellationToken ct
        )
        {
            var container = new WebRequestsContainer();
            await settingsContainer.InitializePluginAsync(container, ct);

            const string CLI_ARG_NAME = "web_requests_mode";

            // Resolve Web Requests Mode
            if (appArgs.TryGetValue(CLI_ARG_NAME, out string? webRequestsMode))
            {
                if (Enum.TryParse(webRequestsMode, true, out WebRequestsMode mode))
                    container.WebRequestsMode = mode;
                else
                {
                    ReportHub.LogWarning(ReportCategory.ENGINE, $"Web Requests Mode {webRequestsMode} is not valid. Defaulting to HTTP2.");
                    container.WebRequestsMode = WebRequestsMode.HTTP2;
                }
            }
            else
                container.WebRequestsMode = container.settings.WebRequestsMode;

            int coreBudget, sceneBudget;

            HTTPManager.Logger.Level = Loglevels.Warning;

            var cacheSize = (ulong)((double)container.settings.Http2Settings.CacheSizeGB * 1024UL * 1024UL * 1024UL);

            // initialize 2 gb cache that will be used for all HTTP2 requests including the special logic for partial ones
            var httpCache = new HTTPCache(new HTTPCacheOptions(TimeSpan.FromDays(container.settings.Http2Settings.CacheLifetimeDays), cacheSize));

            if (container.WebRequestsMode != WebRequestsMode.UNITY)
            {
                coreBudget = container.settings.Http2Settings.CoreWebRequestsBudget;
                sceneBudget = container.settings.Http2Settings.SceneWebRequestsBudget;
            }
            else
            {
                coreBudget = container.settings.DefaultSettings.CoreWebRequestsBudget;
                sceneBudget = container.settings.DefaultSettings.SceneWebRequestsBudget;
            }

            var options = new ArtificialDelayOptions.ElementBindingOptions();

            var analyticsContainer = WebRequestsAnalyticsContainer.Create(debugContainerBuilder.TryAddWidget("Web Requests"));

            var requestCompleteDebugMetric = new ElementBinding<ulong>(0);

            var cannotConnectToHostExceptionDebugMetric = new ElementBinding<ulong>(0);
            var coreAvailableBudget = new ElementBinding<ulong>((ulong)coreBudget);
            var sceneAvailableBudget = new ElementBinding<ulong>((ulong)sceneBudget);

            int partialChunkSize = container.settings.Http2Settings.PartialChunkSizeMB * 1024 * 1024;

            var requestHub = new RequestHub(urlsSource, httpCache, container.EnablePartialDownloading, partialChunkSize, container.settings.Http2Settings.PartialChunksMaxCount, ktxEnabled, container.WebRequestsMode);
            container.requestHub = requestHub;

            IWebRequestController baseWebRequestController = new RedirectWebRequestController(container.WebRequestsMode,
                                                                 new DefaultWebRequestController(analyticsContainer, web3IdentityProvider, requestHub),
                                                                 new Http2WebRequestController(analyticsContainer, web3IdentityProvider, requestHub),
                                                                 new YetAnotherWebRequestController(analyticsContainer, web3IdentityProvider, requestHub),
                                                                 requestHub)
                                                            .WithLog()
                                                            .WithArtificialDelay(options);

            IWebRequestController coreWebRequestController = baseWebRequestController.WithBudget(coreBudget, coreAvailableBudget);
            IWebRequestController sceneWebRequestController = baseWebRequestController.WithBudget(sceneBudget, sceneAvailableBudget);

            CreateStressTestUtility();
            CreateWebRequestDelayUtility();
            CreateWebRequestsMetricsDebugUtility();

            container.AnalyticsContainer = analyticsContainer;
            container.WebRequestController = coreWebRequestController;
            container.SceneWebRequestController = sceneWebRequestController;
            return container;

            void CreateWebRequestsMetricsDebugUtility()
            {
                debugContainerBuilder
                   .TryAddWidget("Web Requests Debug Metrics")
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

        public void SetKTXEnabled(bool enabled)
        {
            // TODO: Temporary until we rewrite FF to be static
            requestHub.SetKTXEnabled(enabled);
        }

        public override void Dispose()
        {
            WebRequestController.Dispose();
            SceneWebRequestController.Dispose();
            HTTPManager.LocalCache?.Dispose();
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public WebRequestsMode WebRequestsMode { get; private set; } = WebRequestsMode.HTTP2;

            [field: SerializeField] public UnityWebRequestsSettings DefaultSettings { get; private set; } = new ();
            [field: SerializeField] public HTTP2Settings Http2Settings { get; private set; } = new ();

            [Serializable]
            public class UnityWebRequestsSettings
            {
                [field: SerializeField] public int CoreWebRequestsBudget { get; private set; } = 15;
                [field: SerializeField] public int SceneWebRequestsBudget { get; private set; } = 5;
            }

            [Serializable]
            public class HTTP2Settings
            {
                [field: SerializeField] public int CoreWebRequestsBudget { get; private set; } = 50;
                [field: SerializeField] public int SceneWebRequestsBudget { get; private set; } = 15;

                [field: SerializeField] public float CacheSizeGB { get; private set; } = 2; // 2 GB by default
                [field: SerializeField] public ushort CacheLifetimeDays { get; private set; } = 2; // 2 days by default
                [field: SerializeField] public short PartialChunkSizeMB { get; private set; } = 2; // 2 MB by default

                /// <summary>
                ///     Splitting a single big asset into too many chunks is not desired as it will lead to creating too many requests and throttling between them that will result in a significantly delayed download.
                /// </summary>
                [field: SerializeField] public byte PartialChunksMaxCount { get; private set; } = 10; // 10 chunks by default
                [field: SerializeField] public ushort PingAckTimeoutSeconds { get; private set; } = 10;
                [field: SerializeField] public bool EnablePartialDownloading { get; private set; }
            }
        }
    }
}
