using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.Browser.DecentralandUrls;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.Web3;
using DCL.Web3.Abstract;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using Segment.Analytics;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Global.Dynamic
{
    public class BootstrapContainer : DCLGlobalContainer<BootstrapSettings>
    {
        private ProvidedAsset<ReportsHandlingSettings> reportHandlingSettings;
        private DiagnosticsContainer? diagnosticsContainer;
        private bool enableAnalytics;

        public IDecentralandUrlsSource DecentralandUrlsSource { get; private set; }
        public IWebBrowser WebBrowser { get; private set; }
        public IWeb3AccountFactory Web3AccountFactory { get; private set; }
        public IAssetsProvisioner? AssetsProvisioner { get; private init; }
        public IBootstrap? Bootstrap { get; private set; }
        public IWeb3IdentityCache? IdentityCache { get; private set; }
        public IVerifiedEthereumApi? VerifiedEthereumApi { get; private set; }
        public IWeb3VerifiedAuthenticator? Web3Authenticator { get; private set; }
        public IAnalyticsController? Analytics { get; private set; }
        public IDebugSettings DebugSettings { get; private set; }
        public IReportsHandlingSettings ReportHandlingSettings => reportHandlingSettings.Value;
        public IAppArgs ApplicationParametersParser { get; private set; }
        public bool LocalSceneDevelopment { get; private set; }
        public bool UseRemoteAssetBundles { get; private set; }

        public override void Dispose()
        {
            base.Dispose();

            diagnosticsContainer?.Dispose();
            reportHandlingSettings.Dispose();
            Web3Authenticator?.Dispose();
            VerifiedEthereumApi?.Dispose();
            IdentityCache?.Dispose();
        }

        public static async UniTask<BootstrapContainer> CreateAsync(
            DebugSettings debugSettings,
            DynamicSceneLoaderSettings sceneLoaderSettings,
            IPluginSettingsContainer settingsContainer,
            RealmLaunchSettings realmLaunchSettings,
            IAppArgs applicationParametersParser,
            World world,
            CancellationToken ct)
        {
            var decentralandUrlsSource = new DecentralandUrlsSource(sceneLoaderSettings.DecentralandEnvironment);
            var browser = new UnityAppWebBrowser(decentralandUrlsSource);
            var web3AccountFactory = new Web3AccountFactory();

            var bootstrapContainer = new BootstrapContainer
            {
                Web3AccountFactory = web3AccountFactory,
                AssetsProvisioner = new AddressablesProvisioner(),
                DecentralandUrlsSource = decentralandUrlsSource,
                WebBrowser = browser,
                LocalSceneDevelopment = realmLaunchSettings.IsLocalSceneDevelopmentRealm || realmLaunchSettings.GetStartingRealm(decentralandUrlsSource) == IRealmNavigator.LOCALHOST,
                UseRemoteAssetBundles = realmLaunchSettings.useRemoteAssetsBundles,
                ApplicationParametersParser = applicationParametersParser,
                DebugSettings = debugSettings,
            };

            await bootstrapContainer.InitializeContainerAsync<BootstrapContainer, BootstrapSettings>(settingsContainer, ct, async container =>
            {
                container.reportHandlingSettings = await ProvideReportHandlingSettingsAsync(container.AssetsProvisioner!, container.settings, ct);
                (container.Bootstrap, container.Analytics) = await CreateBootstrapperAsync(debugSettings, applicationParametersParser, container, container.settings, realmLaunchSettings, world, ct);
                (container.IdentityCache, container.VerifiedEthereumApi, container.Web3Authenticator) = CreateWeb3Dependencies(sceneLoaderSettings, web3AccountFactory, browser, container, decentralandUrlsSource);

                container.diagnosticsContainer = container.enableAnalytics
                    ? DiagnosticsContainer.Create(container.ReportHandlingSettings, realmLaunchSettings.IsLocalSceneDevelopmentRealm, (ReportHandler.DebugLog, new CriticalLogsAnalyticsHandler(container.Analytics)))
                    : DiagnosticsContainer.Create(container.ReportHandlingSettings);
            });

            return bootstrapContainer;
        }

        private static async UniTask<(IBootstrap, IAnalyticsController)> CreateBootstrapperAsync(IDebugSettings debugSettings,
            IAppArgs appArgs,
            BootstrapContainer container,
            BootstrapSettings bootstrapSettings,
            RealmLaunchSettings realmLaunchSettings,
            World world,
            CancellationToken ct)
        {
            AnalyticsConfiguration analyticsConfig = (await container.AssetsProvisioner.ProvideMainAssetAsync(bootstrapSettings.AnalyticsConfigRef, ct)).Value;
            container.enableAnalytics = analyticsConfig.Mode != AnalyticsMode.DISABLED;

            var coreBootstrap = new Bootstrap(debugSettings, appArgs, container.DecentralandUrlsSource, realmLaunchSettings, world)
            {
                EnableAnalytics = container.enableAnalytics,
            };

            if (container.enableAnalytics)
            {
                IAnalyticsService service = CreateAnalyticsService(analyticsConfig);

                appArgs.TryGetValue("launcher_anonymous_id", out string? launcherAnonymousId);
                appArgs.TryGetValue("session_id", out string? sessionId);

                LauncherTraits launcherTraits = new LauncherTraits
                {
                    LauncherAnonymousId = launcherAnonymousId!,
                    SessionId = sessionId!,
                };

                var analyticsController = new AnalyticsController(service, analyticsConfig, launcherTraits);

                return (new BootstrapAnalyticsDecorator(coreBootstrap, analyticsController), analyticsController);
            }

            return (coreBootstrap, IAnalyticsController.Null);
        }

        private static IAnalyticsService CreateAnalyticsService(AnalyticsConfiguration analyticsConfig)
        {
            // Force segment in release
            if (!Debug.isDebugBuild)
                return CreateSegmentAnalyticsOrFallbackToDebug(analyticsConfig);

            return analyticsConfig.Mode switch
                   {
                       AnalyticsMode.SEGMENT => CreateSegmentAnalyticsOrFallbackToDebug(analyticsConfig),
                       AnalyticsMode.DEBUG_LOG => new DebugAnalyticsService(),
                       AnalyticsMode.DISABLED => throw new InvalidOperationException("Trying to create analytics when it is disabled"),
                       _ => throw new ArgumentOutOfRangeException(),
                   };
        }

        private static IAnalyticsService CreateSegmentAnalyticsOrFallbackToDebug(AnalyticsConfiguration analyticsConfig)
        {
            if (analyticsConfig.TryGetSegmentConfiguration(out Configuration segmentConfiguration))
                return new SegmentAnalyticsService(segmentConfiguration);

            // Fall back to debug if segment is not configured
            ReportHub.LogWarning(ReportCategory.ANALYTICS, $"Segment configuration not found. Falling back to {nameof(DebugAnalyticsService)}.");
            return new DebugAnalyticsService();
        }

        private static (
            LogWeb3IdentityCache identityCache,
            IVerifiedEthereumApi web3VerifiedAuthenticator,
            IWeb3VerifiedAuthenticator web3Authenticator
            )
            CreateWeb3Dependencies(
                DynamicSceneLoaderSettings sceneLoaderSettings,
                IWeb3AccountFactory web3AccountFactory,
                IWebBrowser webBrowser,
                BootstrapContainer container,
                IDecentralandUrlsSource decentralandUrlsSource
            )
        {
            var identityCache = new LogWeb3IdentityCache(
                new ProxyIdentityCache(
                    new MemoryWeb3IdentityCache(),
                    new PlayerPrefsIdentityProvider(
                        new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer(
                            web3AccountFactory
                        )
                    )
                )
            );

            var dappWeb3Authenticator = new DappWeb3Authenticator(
                webBrowser,
                decentralandUrlsSource.Url(DecentralandUrl.ApiAuth),
                decentralandUrlsSource.Url(DecentralandUrl.AuthSignature),
                identityCache,
                web3AccountFactory,
                new HashSet<string>(sceneLoaderSettings.Web3WhitelistMethods)
            );

            IWeb3VerifiedAuthenticator coreWeb3Authenticator = new ProxyVerifiedWeb3Authenticator(dappWeb3Authenticator, identityCache);

            if (container.enableAnalytics)
                coreWeb3Authenticator = new IdentityAnalyticsDecorator(coreWeb3Authenticator, container.Analytics!);

            return (identityCache, dappWeb3Authenticator, coreWeb3Authenticator);
        }

        public static async UniTask<ProvidedAsset<ReportsHandlingSettings>> ProvideReportHandlingSettingsAsync(IAssetsProvisioner assetsProvisioner, BootstrapSettings settings, CancellationToken ct)
        {
            BootstrapSettings.ReportHandlingSettingsRef reportHandlingSettings =
#if (DEVELOPMENT_BUILD || UNITY_EDITOR) && !ENABLE_PROFILING
                settings.ReportHandlingSettingsDevelopment;
#else
                settings.ReportHandlingSettingsProduction;
#endif

            return await assetsProvisioner.ProvideMainAssetAsync(reportHandlingSettings, ct, nameof(ReportHandlingSettings));
        }
    }

    [Serializable]
    public class BootstrapSettings : IDCLPluginSettings
    {
        [field: SerializeField] public AnalyticsConfigurationRef AnalyticsConfigRef;
        [field: SerializeField]
        public ReportHandlingSettingsRef ReportHandlingSettingsDevelopment { get; private set; }

        [field: SerializeField]
        public ReportHandlingSettingsRef ReportHandlingSettingsProduction { get; private set; }

        [Serializable]
        public class ReportHandlingSettingsRef : AssetReferenceT<ReportsHandlingSettings>
        {
            public ReportHandlingSettingsRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class AnalyticsConfigurationRef : AssetReferenceT<AnalyticsConfiguration>
        {
            public AnalyticsConfigurationRef(string guid) : base(guid) { }
        }
    }
}
