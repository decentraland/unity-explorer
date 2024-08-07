using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.Browser.DecentralandUrls;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using Segment.Analytics;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Text.RegularExpressions;
using System.Web;

namespace Global.Dynamic
{
    public class BootstrapContainer : DCLGlobalContainer<BootstrapSettings>
    {
        private const string APP_PARAMETER_REALM = "realm";
        private const string APP_PARAMETER_LOCAL_SCENE = "local-scene";
        private const string APP_PARAMETER_POSITION = "position";

        private ProvidedAsset<ReportsHandlingSettings> reportHandlingSettings;
        private DiagnosticsContainer? diagnosticsContainer;
        private static Dictionary<string, string> appParameters = new ();
        private static bool localSceneDevelopment;
        private bool enableAnalytics;

        public IDecentralandUrlsSource DecentralandUrlsSource { get; private set; }
        public IWebBrowser WebBrowser { get; private set; }
        public IAssetsProvisioner? AssetsProvisioner { get; private init; }
        public IBootstrap? Bootstrap { get; private set; }
        public IWeb3IdentityCache? IdentityCache { get; private set; }
        public IVerifiedEthereumApi? VerifiedEthereumApi { get; private set; }
        public IWeb3VerifiedAuthenticator? Web3Authenticator { get; private set; }
        public IAnalyticsController? Analytics { get; private set; }
        public IReportsHandlingSettings ReportHandlingSettings => reportHandlingSettings.Value;
        public bool LocalSceneDevelopment { get; private set; }

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
            RealmLaunchSettings launchSettings,
            CancellationToken ct
        )
        {
            var decentralandUrlsSource = new DecentralandUrlsSource(sceneLoaderSettings.DecentralandEnvironment);
            var browser = new UnityAppWebBrowser(decentralandUrlsSource);

            appParameters = ParseApplicationParameters();
            if (appParameters.ContainsKey(APP_PARAMETER_REALM))
                ProcessRealmAppParameter(launchSettings);
            if (appParameters.ContainsKey(APP_PARAMETER_POSITION))
                ProcessPositionAppParameter(appParameters[APP_PARAMETER_POSITION], launchSettings);
            localSceneDevelopment |= launchSettings.GetStartingRealm() == IRealmNavigator.LOCALHOST;

            var bootstrapContainer = new BootstrapContainer
            {
                AssetsProvisioner = new AddressablesProvisioner(),
                DecentralandUrlsSource = decentralandUrlsSource,
                WebBrowser = browser,
                LocalSceneDevelopment = localSceneDevelopment
            };

            await bootstrapContainer.InitializeContainerAsync<BootstrapContainer, BootstrapSettings>(settingsContainer, ct, async container =>
            {
                container.reportHandlingSettings = await ProvideReportHandlingSettingsAsync(container.AssetsProvisioner!, container.settings, ct);
                (container.Bootstrap, container.Analytics) = await CreateBootstrapperAsync(debugSettings, container, container.settings, ct);
                (container.IdentityCache, container.VerifiedEthereumApi, container.Web3Authenticator) = CreateWeb3Dependencies(sceneLoaderSettings, browser, container);

                container.diagnosticsContainer = container.enableAnalytics
                    ? DiagnosticsContainer.Create(container.ReportHandlingSettings, container.LocalSceneDevelopment, (ReportHandler.DebugLog, new CriticalLogsAnalyticsHandler(container.Analytics)))
                    : DiagnosticsContainer.Create(container.ReportHandlingSettings, container.LocalSceneDevelopment);
            });

            return bootstrapContainer;
        }

        private static async UniTask<(IBootstrap, IAnalyticsController?)> CreateBootstrapperAsync(DebugSettings debugSettings, BootstrapContainer container, BootstrapSettings bootstrapSettings, CancellationToken ct)
        {
            AnalyticsConfiguration analyticsConfig = (await container.AssetsProvisioner.ProvideMainAssetAsync(bootstrapSettings.AnalyticsConfigRef, ct)).Value;
            container.enableAnalytics = analyticsConfig.Mode != AnalyticsMode.DISABLED;

            var coreBootstrap = new Bootstrap(debugSettings.Get())
            {
                EnableAnalytics = container.enableAnalytics,
            };

            if (container.enableAnalytics)
            {
                IAnalyticsService service = CreateAnalyticsService(analyticsConfig);

                var analyticsController = new AnalyticsController(service, analyticsConfig);

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

        private static (LogWeb3IdentityCache identityCache, IVerifiedEthereumApi web3VerifiedAuthenticator, IWeb3VerifiedAuthenticator web3Authenticator)
            CreateWeb3Dependencies(DynamicSceneLoaderSettings sceneLoaderSettings, IWebBrowser webBrowser, BootstrapContainer container)
        {
            var identityCache = new LogWeb3IdentityCache(
                new ProxyIdentityCache(
                    new MemoryWeb3IdentityCache(),
                    new PlayerPrefsIdentityProvider(
                        new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer()
                    )
                )
            );

            var dappWeb3Authenticator = new DappWeb3Authenticator(
                webBrowser,
                GetAuthUrl(sceneLoaderSettings.AuthWebSocketUrl, sceneLoaderSettings.AuthWebSocketUrlDev),
                GetAuthUrl(sceneLoaderSettings.AuthSignatureUrl, sceneLoaderSettings.AuthSignatureUrlDev),
                identityCache, new HashSet<string>(sceneLoaderSettings.Web3WhitelistMethods));

            IWeb3VerifiedAuthenticator coreWeb3Authenticator = new ProxyVerifiedWeb3Authenticator(dappWeb3Authenticator, identityCache);

            if (container.enableAnalytics)
                coreWeb3Authenticator = new IdentityAnalyticsDecorator(coreWeb3Authenticator, container.Analytics!);

            return (identityCache, dappWeb3Authenticator, coreWeb3Authenticator);

            // Allow devUrl only in DebugBuilds (Debug.isDebugBuild is always true in Editor)
            string GetAuthUrl(string releaseUrl, string devUrl) =>
                Application.isEditor || !Debug.isDebugBuild ? releaseUrl : devUrl;
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

        private static Dictionary<string, string> ParseApplicationParameters()
        {
            string[] cmdArgs = Environment.GetCommandLineArgs();

            var deepLinkFound = false;
            string lastKeyStored = string.Empty;

            for (int i = 0; i < cmdArgs.Length; i++)
            {
                string arg = cmdArgs[i];

                if (arg.StartsWith("--"))
                {
                    if (arg.Length > 2)
                    {
                        lastKeyStored = arg.Substring(2);
                        appParameters[lastKeyStored] = string.Empty;
                    }
                    else
                        lastKeyStored = string.Empty;
                }
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
                else if (!deepLinkFound && arg.StartsWith("decentraland://"))
                {
                    deepLinkFound = true;
                    lastKeyStored = string.Empty;

                    // When started in local scene development mode (AKA preview mode) command line arguments are used
                    // Example (Windows) -> start decentraland://"realm=http://127.0.0.1:8000&position=100,100&otherparam=blahblah"
                    ProcessDeepLinkParameters(arg);
                }
#endif
                else if (!string.IsNullOrEmpty(lastKeyStored))
                    appParameters[lastKeyStored] = arg;
            }

            // in MacOS the deep link string doesn't come in the cmd args...
#if !UNITY_EDITOR && UNITY_STANDALONE_OSX
            if (!string.IsNullOrEmpty(Application.absoluteURL) && Application.absoluteURL.StartsWith("decentraland"))
            {
                // Regex patch for MacOS removing the ':' from the realm parameter protocol
                ProcessDeepLinkParameters(Regex.Replace(Application.absoluteURL, @"(https?)//(.*?)$", @"$1://$2"));
            }
#endif

            return appParameters;
        }

        private static void ProcessDeepLinkParameters(string deepLinkString)
        {
            // Update deep link so that Uri class allows the host name
            deepLinkString = Regex.Replace(deepLinkString, @"^decentraland:/+", "https://decentraland.com/?");

            if (!Uri.TryCreate(deepLinkString, UriKind.Absolute, out Uri? res)) return;

            var uri = new Uri(deepLinkString);
            NameValueCollection uriQuery = HttpUtility.ParseQueryString(uri.Query);

            foreach (string uriQueryKey in uriQuery.AllKeys) { appParameters[uriQueryKey] = uriQuery.Get(uriQueryKey); }
        }

        private static void ProcessRealmAppParameter(RealmLaunchSettings launchSettings)
        {
            string realmParamValue = appParameters[APP_PARAMETER_REALM];

            if (string.IsNullOrEmpty(realmParamValue)) return;

            localSceneDevelopment = appParameters.TryGetValue(APP_PARAMETER_LOCAL_SCENE, out string localSceneParamValue)
                                    && ParseLocalSceneParameter(localSceneParamValue)
                                    && IsRealmAValidUrl(realmParamValue);

            if (localSceneDevelopment)
                launchSettings.SetLocalSceneDevelopmentRealm(realmParamValue);
            else if (IsRealmAWorld(realmParamValue))
                launchSettings.SetWorldRealm(realmParamValue);
        }

        private static void ProcessPositionAppParameter(string positionParameterValue, RealmLaunchSettings launchSettings)
        {
            if (string.IsNullOrEmpty(positionParameterValue)) return;

            Vector2Int targetPosition = Vector2Int.zero;

            MatchCollection matches = new Regex(@"-*\d+").Matches(positionParameterValue);

            if (matches.Count > 1)
            {
                targetPosition.x = int.Parse(matches[0].Value);
                targetPosition.y = int.Parse(matches[1].Value);
            }

            launchSettings.SetTargetScene(targetPosition);
        }

        private static bool ParseLocalSceneParameter(string localSceneParameter)
        {
            if (string.IsNullOrEmpty(localSceneParameter)) return false;

            var returnValue = false;
            Match match = new Regex(@"true|false").Match(localSceneParameter);

            if (match.Success)
                bool.TryParse(match.Value, out returnValue);

            return returnValue;
        }

        private static bool IsRealmAWorld(string realmParam) =>
            realmParam.IsEns();

        private static bool IsRealmAValidUrl(string realmParam) =>
            Uri.TryCreate(realmParam, UriKind.Absolute, out Uri? uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
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
