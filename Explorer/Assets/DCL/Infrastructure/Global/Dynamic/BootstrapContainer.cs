using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PerformanceAndDiagnostics.Analytics.Services;
using DCL.PluginSystem;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Settings;
using DCL.Web3;
using DCL.Web3.Abstract;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using Plugins.RustSegment.SegmentServerWrap;
using Global.Dynamic.DebugSettings;
using Global.Dynamic.LaunchModes;
using Global.Dynamic.RealmUrl;
using Global.Versioning;
using Segment.Analytics;
using Sentry;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.UI.SceneDebugConsole.MessageBus;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Global.Dynamic
{
    public class BootstrapContainer : DCLGlobalContainer<BootstrapSettings>
    {
        private ProvidedAsset<ReportsHandlingSettings> reportHandlingSettings;
        private bool enableAnalytics;

        public DiagnosticsContainer DiagnosticsContainer { get; private set; }
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
        public WorldVolumeMacBus WorldVolumeMacBus { get; private set; }
        public IReportsHandlingSettings ReportHandlingSettings => reportHandlingSettings.Value;
        public IAppArgs ApplicationParametersParser { get; private set; }
        public ILaunchMode LaunchMode { get; private set; }
        public bool UseRemoteAssetBundles { get; private set; }
        public SceneDebugConsoleLogEntryBus? SceneDebugConsoleMessageBus { get; private set; }

        public DecentralandEnvironment Environment { get; private set; }

        public override void Dispose()
        {
            base.Dispose();

            DiagnosticsContainer?.Dispose();
            reportHandlingSettings.Dispose();
            Web3Authenticator?.Dispose();
            VerifiedEthereumApi?.Dispose();
            IdentityCache?.Dispose();
        }

        public static async UniTask<BootstrapContainer> CreateAsync(
            DebugSettings.DebugSettings debugSettings,
            DynamicSceneLoaderSettings sceneLoaderSettings,
            IDecentralandUrlsSource decentralandUrlsSource,
            WebRequestsContainer webRequestsContainer,
            IWeb3IdentityCache identityCache,
            IPluginSettingsContainer settingsContainer,
            RealmLaunchSettings realmLaunchSettings,
            IAppArgs applicationParametersParser,
            ISplashScreen splashScreen,
            IRealmUrls realmUrls,
            IDiskCache diskCache,
            IDiskCache<PartialLoadingState> partialsDiskCache,
            World world,
            DecentralandEnvironment decentralandEnvironment,
            DCLVersion dclVersion,
            CancellationToken ct)
        {
            var browser = new UnityAppWebBrowser(decentralandUrlsSource);
            var web3AccountFactory = new Web3AccountFactory();

            bool enableSceneDebugConsole = realmLaunchSettings.CurrentMode is LaunchModes.LaunchMode.LocalSceneDevelopment || applicationParametersParser.HasFlag(AppArgsFlags.SCENE_CONSOLE);
            var sceneDebugConsoleMessageBus = enableSceneDebugConsole ? new SceneDebugConsoleLogEntryBus() : null;

            var bootstrapContainer = new BootstrapContainer
            {
                IdentityCache = identityCache,
                Web3AccountFactory = web3AccountFactory,
                AssetsProvisioner = new AddressablesProvisioner(),
                DecentralandUrlsSource = decentralandUrlsSource,
                WebBrowser = browser,
                LaunchMode = realmLaunchSettings,
                UseRemoteAssetBundles = realmLaunchSettings.useRemoteAssetsBundles,
                ApplicationParametersParser = applicationParametersParser,
                DebugSettings = debugSettings,
                WorldVolumeMacBus = new WorldVolumeMacBus(),
                Environment = decentralandEnvironment,
                SceneDebugConsoleMessageBus = sceneDebugConsoleMessageBus
            };

            await bootstrapContainer.InitializeContainerAsync<BootstrapContainer, BootstrapSettings>(settingsContainer, ct, async container =>
            {
                container.reportHandlingSettings = await ProvideReportHandlingSettingsAsync(container.AssetsProvisioner!, container.settings, ct);

                (container.Bootstrap, container.Analytics) = await CreateBootstrapperAsync(debugSettings, applicationParametersParser, splashScreen, realmUrls, diskCache, partialsDiskCache, container, webRequestsContainer, container.settings, realmLaunchSettings, world, container.settings.BuildData, dclVersion, ct);
                (container.VerifiedEthereumApi, container.Web3Authenticator) = CreateWeb3Dependencies(sceneLoaderSettings, web3AccountFactory, identityCache, browser, container, decentralandUrlsSource, applicationParametersParser);

                if (container.enableAnalytics)
                {
                    container.Analytics!.Initialize(container.IdentityCache.Identity);

                    CrashDetector.Initialize(container.Analytics);
                }

                container.DiagnosticsContainer = DiagnosticsContainer.Create(container.ReportHandlingSettings, sceneDebugConsoleMessageBus);
                container.DiagnosticsContainer.AddSentryScopeConfigurator(AddIdentityToSentryScope);

                void AddIdentityToSentryScope(Scope scope)
                {
                    if (container.IdentityCache.Identity != null)
                        container.DiagnosticsContainer.Sentry!.AddIdentityToScope(scope, container.IdentityCache.Identity.Address);
                }
            });

            return bootstrapContainer;
        }

        private static async UniTask<(IBootstrap, IAnalyticsController)> CreateBootstrapperAsync(
            IDebugSettings debugSettings,
            IAppArgs appArgs,
            ISplashScreen splashScreen,
            IRealmUrls realmUrls,
            IDiskCache diskCache,
            IDiskCache<PartialLoadingState> partialsDiskCache,
            BootstrapContainer container,
            WebRequestsContainer webRequestsContainer,
            BootstrapSettings bootstrapSettings,
            RealmLaunchSettings realmLaunchSettings,
            World world,
            BuildData buildData,
            DCLVersion dclVersion,
            CancellationToken ct)
        {
            AnalyticsConfiguration analyticsConfig = (await container.AssetsProvisioner.ProvideMainAssetAsync(bootstrapSettings.AnalyticsConfigRef, ct)).Value;
            container.enableAnalytics = analyticsConfig.Mode != AnalyticsMode.DISABLED;

            var coreBootstrap = new Bootstrap(debugSettings, appArgs, splashScreen, realmUrls, realmLaunchSettings, webRequestsContainer, diskCache, partialsDiskCache, world)
            {
                EnableAnalytics = container.enableAnalytics,
            };

            if (container.enableAnalytics)
            {
                LauncherTraits launcherTraits = LauncherTraits.FromAppArgs(appArgs);
                IAnalyticsService service = CreateAnalyticsService(
                    analyticsConfig,
                    launcherTraits,
                    container.ApplicationParametersParser,
                    realmLaunchSettings.CurrentMode is LaunchModes.LaunchMode.LocalSceneDevelopment,
                    ct);

                var analyticsController = new AnalyticsController(service, appArgs, analyticsConfig, launcherTraits, buildData, dclVersion);
                var criticalLogsAnalyticsHandler = new CriticalLogsAnalyticsHandler(analyticsController);

                return (new BootstrapAnalyticsDecorator(coreBootstrap, analyticsController), analyticsController);
            }

            return (coreBootstrap, IAnalyticsController.Null);
        }

        private static IAnalyticsService CreateAnalyticsService(AnalyticsConfiguration analyticsConfig, LauncherTraits launcherTraits, IAppArgs args, bool isLocalSceneDevelopment, CancellationToken token)
        {
            // Avoid Segment analytics for: Unity Editor or Debug Mode (except when in Local Scene Development mode)
#if !UNITY_EDITOR
            if (!args.HasDebugFlag() || isLocalSceneDevelopment)
                return CreateSegmentAnalyticsOrFallbackToDebug(analyticsConfig, launcherTraits, token);
#endif

            return analyticsConfig.Mode switch
                   {
                       AnalyticsMode.SEGMENT => CreateSegmentAnalyticsOrFallbackToDebug(analyticsConfig, launcherTraits, token),
                       AnalyticsMode.DEBUG_LOG => new DebugAnalyticsService(),
                       AnalyticsMode.DISABLED => throw new InvalidOperationException("Trying to create analytics when it is disabled"),
                       _ => throw new ArgumentOutOfRangeException(),
                   };
        }

        private static IAnalyticsService CreateSegmentAnalyticsOrFallbackToDebug(AnalyticsConfiguration analyticsConfig, LauncherTraits launcherTraits, CancellationToken token)
        {
            if (analyticsConfig.TryGetSegmentConfiguration(out Configuration segmentConfiguration))
                return new RustSegmentAnalyticsService(segmentConfiguration.WriteKey!, launcherTraits.LauncherAnonymousId)
                   .WithTimeFlush(TimeSpan.FromSeconds(analyticsConfig.FlushInterval), token);

            // Fall back to debug if segment is not configured
            ReportHub.LogWarning(ReportCategory.ANALYTICS, $"Segment configuration not found. Falling back to {nameof(DebugAnalyticsService)}.");
            return new DebugAnalyticsService();
        }

        private static (IVerifiedEthereumApi web3VerifiedAuthenticator, IWeb3VerifiedAuthenticator web3Authenticator)
            CreateWeb3Dependencies(
                DynamicSceneLoaderSettings sceneLoaderSettings,
                IWeb3AccountFactory web3AccountFactory,
                IWeb3IdentityCache identityCache,
                IWebBrowser webBrowser,
                BootstrapContainer container,
                IDecentralandUrlsSource decentralandUrlsSource,
                IAppArgs appArgs)
        {
            var dappWeb3Authenticator = new DappWeb3Authenticator(
                webBrowser,
                URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.ApiAuth)),
                URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.AuthSignatureWebApp)),
                URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.ApiRpc)),
                identityCache,
                web3AccountFactory,
                new HashSet<string>(sceneLoaderSettings.Web3WhitelistMethods),
                new HashSet<string>(sceneLoaderSettings.Web3ReadOnlyMethods),
                decentralandUrlsSource.Environment,
                appArgs.TryGetValue(AppArgsFlags.IDENTITY_EXPIRATION_DURATION, out string? v) ? int.Parse(v!) : null
            );

            IWeb3VerifiedAuthenticator coreWeb3Authenticator = new ProxyVerifiedWeb3Authenticator(dappWeb3Authenticator, identityCache);

            if (container.enableAnalytics)
                coreWeb3Authenticator = new IdentityAnalyticsDecorator(coreWeb3Authenticator, container.Analytics!);

            return (dappWeb3Authenticator, coreWeb3Authenticator);
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
        [field: SerializeField] public ReportHandlingSettingsRef ReportHandlingSettingsDevelopment { get; private set; }
        [field: SerializeField] public ReportHandlingSettingsRef ReportHandlingSettingsProduction { get; private set; }
        [field: SerializeField] public BuildData BuildData { get; private set; }

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
