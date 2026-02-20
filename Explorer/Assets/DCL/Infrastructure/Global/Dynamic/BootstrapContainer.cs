using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Browser;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Utility;
using DCL.Web3.Abstract;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.ChromeDevtool;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using Global.Dynamic.RealmUrl;
using Global.Dynamic.RealmUrl.Names;
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

        public bool EnableAnalytics => Analytics.Enabled;
        public DiagnosticsContainer DiagnosticsContainer { get; private set; }
        public IDecentralandUrlsSource DecentralandUrlsSource { get; private set; }
        public IWebBrowser WebBrowser { get; private set; }
        public IWeb3AccountFactory Web3AccountFactory { get; private set; }
        public IAssetsProvisioner? AssetsProvisioner { get; private init; }
        public IBootstrap? Bootstrap { get; private set; }
        public IWeb3IdentityCache? IdentityCache { get; private set; }
        public ICompositeWeb3Provider? CompositeWeb3Provider { get; private set; }
        public AnalyticsContainer Analytics { get; private set; }
        public DebugSettings.DebugSettings DebugSettings { get; private set; }
        public VolumeBus VolumeBus { get; private set; }
        public IReportsHandlingSettings ReportHandlingSettings => reportHandlingSettings;
        public IAppArgs AppArgs { get; private set; }
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
            Analytics?.Dispose();
        }

        public static async UniTask<BootstrapContainer> CreateAsync(
            IAssetsProvisioner assetsProvisioner,
            DebugSettings.DebugSettings debugSettings,
            DynamicSceneLoaderSettings sceneLoaderSettings,
            IDecentralandUrlsSource decentralandUrlsSource,
            DebugUtilitiesContainer debugContainer,
            IWeb3IdentityCache identityCache,
            IPluginSettingsContainer settingsContainer,
            RealmLaunchSettings realmLaunchSettings,
            IAppArgs applicationParametersParser,
            SplashScreen splashScreen,
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
                AppArgs = applicationParametersParser,
                DebugSettings = debugSettings,
                VolumeBus = new VolumeBus(),
                Environment = decentralandEnvironment
            };

            await bootstrapContainer.InitializeContainerAsync<BootstrapContainer, BootstrapSettings>(settingsContainer, ct, async container =>
            {
                container.reportHandlingSettings = ProvideReportHandlingSettingsAsync(container.settings, applicationParametersParser);

                container.DiagnosticsContainer = DiagnosticsContainer.Create(container.ReportHandlingSettings);
                container.DiagnosticsContainer.AddSentryScopeConfigurator(AddIdentityToSentryScope);

                var cdpClient = ChromeDevToolHandler.New(applicationParametersParser.HasFlag(AppArgsFlags.LAUNCH_CDP_MONITOR_ON_START), applicationParametersParser);
                WebRequestsContainer? webRequestsContainer = await WebRequestsContainer.CreateAsync(settingsContainer, identityCache, debugContainer.Builder, decentralandUrlsSource, cdpClient, container.DiagnosticsContainer.SentrySampler, ct);
                var realmUrls = new RealmUrls(realmLaunchSettings, new RealmNamesMap(webRequestsContainer.WebRequestController), decentralandUrlsSource);

                container.Bootstrap = await CreateBootstrapperAsync(debugSettings, debugContainer, applicationParametersParser, splashScreen, realmUrls, diskCache, partialsDiskCache, container, webRequestsContainer, settingsContainer, realmLaunchSettings, world, container.settings.BuildData, dclVersion, ct);
                container.CompositeWeb3Provider = CreateWeb3Dependencies(sceneLoaderSettings, web3AccountFactory, identityCache, browser, container.Analytics, decentralandUrlsSource, decentralandEnvironment, applicationParametersParser);

                void AddIdentityToSentryScope(Scope scope)
                {
                    if (container.IdentityCache.Identity != null)
                        container.DiagnosticsContainer.Sentry!.AddIdentityToScope(scope, container.IdentityCache.Identity.Address);
                }
            });

            return bootstrapContainer;
        }

        private static async UniTask<IBootstrap> CreateBootstrapperAsync(
            DebugSettings.DebugSettings debugSettings,
            DebugUtilitiesContainer debugUtilitiesContainer,
            IAppArgs appArgs,
            SplashScreen splashScreen,
            RealmUrls realmUrls,
            IDiskCache diskCache,
            IDiskCache<PartialLoadingState> partialsDiskCache,
            BootstrapContainer container,
            WebRequestsContainer webRequestsContainer,
            IPluginSettingsContainer settingsContainer,
            RealmLaunchSettings realmLaunchSettings,
            World world,
            BuildData buildData,
            DCLVersion dclVersion,
            CancellationToken ct)
        {
            AnalyticsContainer? analyticsContainer = await AnalyticsContainer.CreateAsync(appArgs, container.IdentityCache, realmLaunchSettings, debugUtilitiesContainer.Builder, buildData, settingsContainer, dclVersion, ct);
            container.Analytics = analyticsContainer;

            var coreBootstrap = new Bootstrap(debugSettings, appArgs, splashScreen, realmUrls, realmLaunchSettings, webRequestsContainer, diskCache, partialsDiskCache,
                new HttpFeatureFlagsProvider(webRequestsContainer.WebRequestController), world)
            {
                EnableAnalytics = analyticsContainer.Enabled,
            };

            if (analyticsContainer.Enabled)
                return new BootstrapAnalyticsDecorator(coreBootstrap, analyticsContainer.Controller);

            return coreBootstrap;
        }



        private static ICompositeWeb3Provider CreateWeb3Dependencies(
            DynamicSceneLoaderSettings sceneLoaderSettings,
            IWeb3AccountFactory web3AccountFactory,
            IWeb3IdentityCache identityCache,
            IWebBrowser webBrowser,
            AnalyticsContainer container,
            IDecentralandUrlsSource decentralandUrlsSource,
            DecentralandEnvironment dclEnvironment,
            IAppArgs appArgs)
        {
            int? identityExpirationDuration = appArgs.TryGetValue(AppArgsFlags.IDENTITY_EXPIRATION_DURATION, out string? v)
                ? int.Parse(v!)
                : null;

            // Create ThirdWeb authenticator (Email + OTP)
            var thirdWebAuth = new ThirdWebAuthenticator(
                decentralandUrlsSource,
                dclEnvironment,
                new HashSet<string>(sceneLoaderSettings.Web3WhitelistMethods),
                new HashSet<string>(sceneLoaderSettings.Web3ReadOnlyMethods),
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

            ICompositeWeb3Provider result = new CompositeWeb3Provider(thirdWebAuth, dappAuth, identityCache, container.Controller);

            return result;
        }

        private static IReportsHandlingSettings ProvideReportHandlingSettingsAsync(BootstrapSettings settings, IAppArgs applicationParametersParser)
        {
#if (DEVELOPMENT_BUILD || UNITY_EDITOR) && !ENABLE_PROFILING
            ReportsHandlingSettings baseSettings = settings.ReportHandlingSettingsDevelopment;
#else
            ReportsHandlingSettings baseSettings = settings.ReportHandlingSettingsProduction;
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
        [field: SerializeField] public ReportsHandlingSettings ReportHandlingSettingsDevelopment { get; private set; }
        [field: SerializeField] public ReportsHandlingSettings ReportHandlingSettingsProduction { get; private set; }
        [field: SerializeField] public BuildData BuildData { get; private set; }
    }
}
