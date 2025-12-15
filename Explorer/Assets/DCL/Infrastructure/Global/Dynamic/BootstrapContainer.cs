using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PerformanceAndDiagnostics.Analytics.Services;
using DCL.PluginSystem;
using DCL.SceneLoadingScreens.SplashScreen;
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
using Global.Dynamic.LaunchModes;
using Global.Dynamic.RealmUrl;
using Global.Versioning;
using Sentry;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Global.Dynamic
{
    public class BootstrapContainer : DCLGlobalContainer<BootstrapSettings>
    {
        private IReportsHandlingSettings reportHandlingSettings;

        public bool EnableAnalytics { get; private set; }
        public DiagnosticsContainer DiagnosticsContainer { get; private set; }
        public IDecentralandUrlsSource DecentralandUrlsSource { get; private set; }
        public IWebBrowser WebBrowser { get; private set; }
        public IWeb3AccountFactory Web3AccountFactory { get; private set; }
        public IAssetsProvisioner? AssetsProvisioner { get; private init; }
        public IBootstrap? Bootstrap { get; private set; }
        public IWeb3IdentityCache? IdentityCache { get; private set; }
        public IEthereumApi? EthereumApi { get; private set; }
        public IWeb3VerifiedAuthenticator? Web3Authenticator { get; private set; }
        public ICompositeWeb3Provider? CompositeWeb3Provider { get; private set; }
        public IAnalyticsController? Analytics { get; private set; }
        public DebugSettings.DebugSettings DebugSettings { get; private set; }
        public VolumeBus VolumeBus { get; private set; }
        public IReportsHandlingSettings ReportHandlingSettings => reportHandlingSettings;
        public IAppArgs ApplicationParametersParser { get; private set; }
        public ILaunchMode LaunchMode { get; private set; }
        public bool UseRemoteAssetBundles { get; private set; }
        public DecentralandEnvironment Environment { get; private set; }

        public override void Dispose()
        {
            base.Dispose();

            DiagnosticsContainer?.Dispose();

            // CompositeWeb3Provider disposes both authenticators internally
            // Don't dispose Web3Authenticator/EthereumApi separately as they reference the same composite
            CompositeWeb3Provider?.Dispose();
            IdentityCache?.Dispose();
        }

        public static async UniTask<BootstrapContainer> CreateAsync(
            IAssetsProvisioner assetsProvisioner,
            DebugSettings.DebugSettings debugSettings,
            DynamicSceneLoaderSettings sceneLoaderSettings,
            IDecentralandUrlsSource decentralandUrlsSource,
            WebRequestsContainer webRequestsContainer,
            IWeb3IdentityCache identityCache,
            IPluginSettingsContainer settingsContainer,
            RealmLaunchSettings realmLaunchSettings,
            IAppArgs applicationParametersParser,
            SplashScreen splashScreen,
            RealmUrls realmUrls,
            IDiskCache diskCache,
            IDiskCache<PartialLoadingState> partialsDiskCache,
            World world,
            DecentralandEnvironment decentralandEnvironment,
            DCLVersion dclVersion,
            CancellationToken ct)
        {
            var browser = new UnityAppWebBrowser(decentralandUrlsSource);
            var web3AccountFactory = new Web3AccountFactory();

            var bootstrapContainer = new BootstrapContainer
            {
                IdentityCache = identityCache,
                Web3AccountFactory = web3AccountFactory,
                AssetsProvisioner = assetsProvisioner,
                DecentralandUrlsSource = decentralandUrlsSource,
                WebBrowser = browser,
                LaunchMode = realmLaunchSettings,
                UseRemoteAssetBundles = realmLaunchSettings.useRemoteAssetsBundles,
                ApplicationParametersParser = applicationParametersParser,
                DebugSettings = debugSettings,
                VolumeBus = new VolumeBus(),
                Environment = decentralandEnvironment
            };

            await bootstrapContainer.InitializeContainerAsync<BootstrapContainer, BootstrapSettings>(settingsContainer, ct, async container =>
            {
                container.reportHandlingSettings = ProvideReportHandlingSettingsAsync(container.settings, applicationParametersParser);

                (container.Bootstrap, container.Analytics) = CreateBootstrapperAsync(debugSettings, applicationParametersParser, splashScreen, realmUrls, diskCache, partialsDiskCache, container, webRequestsContainer, container.settings, realmLaunchSettings, world, container.settings.BuildData, dclVersion, ct);
                (container.EthereumApi, container.Web3Authenticator, container.CompositeWeb3Provider) = CreateWeb3Dependencies(sceneLoaderSettings, web3AccountFactory, identityCache, browser, container, decentralandUrlsSource, decentralandEnvironment, applicationParametersParser);

                if (container.EnableAnalytics)
                {
                    container.Analytics!.Initialize(container.IdentityCache.Identity);
                    CrashDetector.Initialize(container.Analytics);
                }

                container.DiagnosticsContainer = DiagnosticsContainer.Create(container.ReportHandlingSettings);
                container.DiagnosticsContainer.AddSentryScopeConfigurator(AddIdentityToSentryScope);

                void AddIdentityToSentryScope(Scope scope)
                {
                    if (container.IdentityCache.Identity != null)
                        container.DiagnosticsContainer.Sentry!.AddIdentityToScope(scope, container.IdentityCache.Identity.Address);
                }
            });

            return bootstrapContainer;
        }

        private static (IBootstrap, IAnalyticsController) CreateBootstrapperAsync(
            DebugSettings.DebugSettings debugSettings,
            IAppArgs appArgs,
            SplashScreen splashScreen,
            RealmUrls realmUrls,
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
            container.EnableAnalytics = bootstrapSettings.AnalyticsConfig.Mode != AnalyticsMode.DISABLED;

            var coreBootstrap = new Bootstrap(debugSettings, appArgs, splashScreen, realmUrls, realmLaunchSettings, webRequestsContainer, diskCache, partialsDiskCache, world)
            {
                EnableAnalytics = container.EnableAnalytics,
            };

            if (container.EnableAnalytics)
            {
                LauncherTraits launcherTraits = LauncherTraits.FromAppArgs(appArgs);
                IAnalyticsService service = CreateAnalyticsService(
                    bootstrapSettings.AnalyticsConfig,
                    launcherTraits,
                    container.ApplicationParametersParser,
                    realmLaunchSettings.CurrentMode is LaunchModes.LaunchMode.LocalSceneDevelopment,
                    ct);

                var analyticsController = new AnalyticsController(service, appArgs, bootstrapSettings.AnalyticsConfig, launcherTraits, buildData, dclVersion);
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

        private static (IEthereumApi ethereumApi, IWeb3VerifiedAuthenticator web3Authenticator, ICompositeWeb3Provider compositeProvider)
            CreateWeb3Dependencies(
                DynamicSceneLoaderSettings sceneLoaderSettings,
                IWeb3AccountFactory web3AccountFactory,
                IWeb3IdentityCache identityCache,
                IWebBrowser webBrowser,
                BootstrapContainer container,
                IDecentralandUrlsSource decentralandUrlsSource,
                DecentralandEnvironment dclEnvironment,
                IAppArgs appArgs)
        {
            int? identityExpirationDuration = appArgs.TryGetValue(AppArgsFlags.IDENTITY_EXPIRATION_DURATION, out string? v)
                ? int.Parse(v!)
                : null;

            // Create ThirdWeb authenticator (Email + OTP)
            var thirdWebAuth = new ThirdWebAuthenticator(
                dclEnvironment,
                identityCache,
                new HashSet<string>(sceneLoaderSettings.Web3WhitelistMethods),
                web3AccountFactory,
                identityExpirationDuration
            );

            // Create Dapp authenticator (Browser wallet)
            var dappAuth = new DappWeb3Authenticator(
                webBrowser,
                URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.ApiAuth)),
                URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.AuthSignatureWebApp)),
                URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.ApiRpc)),
                identityCache,
                web3AccountFactory,
                new HashSet<string>(sceneLoaderSettings.Web3WhitelistMethods),
                new HashSet<string>(sceneLoaderSettings.Web3ReadOnlyMethods),
                dclEnvironment,
                new AuthCodeVerificationFeatureFlag(),
                identityExpirationDuration
            );

            // Create composite provider that wraps both
            var compositeProvider = new CompositeWeb3Provider(thirdWebAuth, dappAuth);

            IWeb3VerifiedAuthenticator coreWeb3Authenticator = new ProxyVerifiedWeb3Authenticator(compositeProvider, identityCache);

            if (container.EnableAnalytics)
                coreWeb3Authenticator = new AnalyticsDecoratorVerifiedAuthenticator(coreWeb3Authenticator, container.Analytics!);

            return (compositeProvider, coreWeb3Authenticator, compositeProvider);
        }

        private static IReportsHandlingSettings ProvideReportHandlingSettingsAsync(BootstrapSettings settings, IAppArgs applicationParametersParser)
        {
            ReportsHandlingSettings baseSettings =
#if (DEVELOPMENT_BUILD || UNITY_EDITOR) && !ENABLE_PROFILING
                settings.ReportHandlingSettingsDevelopment;
#else
                settings.ReportHandlingSettingsProduction;
#endif

            IReportsHandlingSettings finalSettings = baseSettings;

            if (applicationParametersParser.TryGetValue(AppArgsFlags.USE_LOG_MATRIX, out string? logMatrixFileName) && !string.IsNullOrEmpty(logMatrixFileName))
            {
                var jsonOverride = LogMatrixJsonLoader.LoadFromApplicationRoot(logMatrixFileName);

                if (jsonOverride != null)
                {
                    ReportHub.LogProductionInfo($"Applying log matrix override from: {logMatrixFileName}");
                    finalSettings = new ReportsHandlingSettingsWithOverride(baseSettings, jsonOverride);
                }
                else
                {
                    ReportHub.LogWarning(ReportCategory.ENGINE, $"Failed to load log matrix override, falling back to base settings");
                }
            }

            return new RuntimeReportsHandlingSettings(finalSettings);
        }
    }

    internal class AuthCodeVerificationFeatureFlag : DappWeb3Authenticator.ICodeVerificationFeatureFlag
    {
        public bool ShouldWaitForCodeVerificationFromServer => FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.AUTH_CODE_VALIDATION);
    }

    [Serializable]
    public class BootstrapSettings : IDCLPluginSettings
    {
        [field: SerializeField] public AnalyticsConfiguration AnalyticsConfig;
        [field: SerializeField] public ReportsHandlingSettings ReportHandlingSettingsDevelopment { get; private set; }
        [field: SerializeField] public ReportsHandlingSettings ReportHandlingSettingsProduction { get; private set; }
        [field: SerializeField] public BuildData BuildData { get; private set; }
    }
}
