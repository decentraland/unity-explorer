using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Input.Component;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using Global.AppArgs;
using LiveKit.Proto;
using SceneRunner.Debugging;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public interface IDebugSettings
    {
        bool ShowSplash { get; }
        bool ShowAuthentication { get; }
        bool ShowLoading { get; }
        bool EnableLandscape { get; }
        bool EnableLOD { get; }
        bool EnableEmulateNoLivekitConnection { get; }
        bool OverrideConnectionQuality { get; }
        ConnectionQuality ConnectionQuality { get; }
    }

    [Serializable]
    public class DebugSettings : IDebugSettings
    {
        private static readonly DebugSettings RELEASE_SETTINGS = Release();

        [SerializeField]
        private bool showSplash;
        [SerializeField]
        private bool showAuthentication;
        [SerializeField]
        private bool showLoading;
        [SerializeField]
        private bool enableLandscape;
        [SerializeField]
        private bool enableLOD;
        [SerializeField]
        private bool enableEmulateNoLivekitConnection;
        [Space]
        [SerializeField]
        private bool overrideConnectionQuality;
        [SerializeField]
        private ConnectionQuality connectionQuality;

        public static DebugSettings Release() =>
            new ()
            {
                showSplash = true,
                showAuthentication = true,
                showLoading = true,
                enableLandscape = true,
                enableLOD = true,
                enableEmulateNoLivekitConnection = false,
                overrideConnectionQuality = false,
                connectionQuality = ConnectionQuality.QualityExcellent
            };

        // To avoid configuration issues, force full flow on build (Debug.isDebugBuild is always true in Editor)
        public bool ShowSplash => Debug.isDebugBuild ? this.showSplash : RELEASE_SETTINGS.showSplash;
        public bool ShowAuthentication => Debug.isDebugBuild ? this.showAuthentication : RELEASE_SETTINGS.showAuthentication;
        public bool ShowLoading => Debug.isDebugBuild ? this.showLoading : RELEASE_SETTINGS.showLoading;
        public bool EnableLandscape => Debug.isDebugBuild ? this.enableLandscape : RELEASE_SETTINGS.enableLandscape;
        public bool EnableLOD => Debug.isDebugBuild ? this.enableLOD : RELEASE_SETTINGS.enableLOD;
        public bool EnableEmulateNoLivekitConnection => Debug.isDebugBuild ? this.enableEmulateNoLivekitConnection : RELEASE_SETTINGS.enableEmulateNoLivekitConnection;
        public bool OverrideConnectionQuality => Debug.isDebugBuild ? this.overrideConnectionQuality : RELEASE_SETTINGS.overrideConnectionQuality;
        public ConnectionQuality ConnectionQuality => Debug.isDebugBuild ? this.connectionQuality : RELEASE_SETTINGS.connectionQuality;
    }

    public class MainSceneLoader : MonoBehaviour
    {
        [System.Serializable]
        private struct GitHubRelease
        {
            public string tag_name;
        }

        [Header("Startup Config")] [SerializeField]
        private RealmLaunchSettings launchSettings = null!;

        [Space]
        [SerializeField] private DebugViewsCatalog debugViewsCatalog = new ();

        [Space]
        [SerializeField] private DebugSettings debugSettings = new ();

        [Header("References")]
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer = null!;
        [SerializeField] private PluginSettingsContainer scenePluginSettingsContainer = null!;
        [SerializeField] private UIDocument uiToolkitRoot = null!;
        [SerializeField] private UIDocument cursorRoot = null!;
        [SerializeField] private UIDocument debugUiRoot = null!;
        [SerializeField] private DynamicSceneLoaderSettings settings = null!;
        [SerializeField] private DynamicSettings dynamicSettings = null!;
        [SerializeField] private GameObject splashRoot = null!;
        [SerializeField] private Animator splashScreenAnimation = null!;
        [SerializeField] private AudioClipConfig backgroundMusic = null!;
        [SerializeField] private WorldInfoTool worldInfoTool = null!;

        private BootstrapContainer? bootstrapContainer;
        private StaticContainer? staticContainer;
        private DynamicWorldContainer? dynamicWorldContainer;
        private GlobalWorld? globalWorld;

        private void Awake()
        {
            InitializeFlowAsync(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            if (dynamicWorldContainer != null)
            {
                foreach (IDCLGlobalPlugin plugin in dynamicWorldContainer.GlobalPlugins)
                    plugin.SafeDispose(ReportCategory.ENGINE);

                if (globalWorld != null)
                    dynamicWorldContainer.RealmController.DisposeGlobalWorld();

                dynamicWorldContainer.SafeDispose(ReportCategory.ENGINE);
            }

            if (staticContainer != null)
            {
                // Exclude SharedPlugins as they were disposed as they were already disposed of as `GlobalPlugins`
                foreach (IDCLPlugin worldPlugin in staticContainer.ECSWorldPlugins.Except<IDCLPlugin>(staticContainer.SharedPlugins))
                    worldPlugin.SafeDispose(ReportCategory.ENGINE);

                staticContainer.SafeDispose(ReportCategory.ENGINE);
            }

            bootstrapContainer?.Dispose();

            ReportHub.Log(ReportCategory.ENGINE, "OnDestroy successfully finished");
        }

        private async UniTask InitializeFlowAsync(CancellationToken ct)
        {
            var applicationParametersParser = new ApplicationParametersParser(Environment.GetCommandLineArgs());

            settings.ApplyConfig(applicationParametersParser);
            launchSettings.ApplyConfig(applicationParametersParser);

            World world = World.Create();

            bootstrapContainer = await BootstrapContainer.CreateAsync(debugSettings, sceneLoaderSettings: settings,
                globalPluginSettingsContainer, launchSettings,
                applicationParametersParser,
                world,
                destroyCancellationToken);

            IBootstrap bootstrap = bootstrapContainer!.Bootstrap!;

            try
            {
                var splashScreen = new SplashScreen(splashScreenAnimation, splashRoot, debugSettings.ShowSplash);

                bootstrap.PreInitializeSetup(cursorRoot, debugUiRoot, splashScreen, destroyCancellationToken);

                bool isLoaded;
                (staticContainer, isLoaded) = await bootstrap.LoadStaticContainerAsync(bootstrapContainer, globalPluginSettingsContainer, debugViewsCatalog, ct);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                const string API_URL = "https://api.github.com/repos/decentraland/unity-explorer/releases/latest";
                var webController = staticContainer.WebRequestsContainer.WebRequestController;
                var response = await webController.GetAsync<FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(API_URL, new FlatFetchResponse<GenericGetRequest>(), ct);

                GitHubRelease latestRelease = JsonUtility.FromJson<GitHubRelease>(response.body);
                string latestVersion = latestRelease.tag_name.TrimStart('v');

                var isOlder = IsOlder(ExtractSemanticVersion(Application.version), ExtractSemanticVersion(latestVersion));

                if (isOlder)
                {

                }

                (int, int, int) ExtractSemanticVersion(string versionString)
                {
                    Match match = Regex.Match(versionString, @"v?(\d+)\.?(\d*)\.?(\d*)");

                    if (!match.Success) return (0, 0, 0); // Default if no version found

                    var major = int.Parse(match.Groups[1].Value);
                    int minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                    int patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
                    return (major, minor, patch);
                }

                bool IsOlder((int, int, int) current, (int, int, int) latest)
                {
                    if (current.Item1 < latest.Item1) return true;
                    if (current.Item2 < latest.Item2) return true;
                    return current.Item3 < latest.Item3;
                }

                Entity playerEntity = bootstrap.CreatePlayerEntity(staticContainer!);

                (dynamicWorldContainer, isLoaded) = await bootstrap.LoadDynamicWorldContainerAsync(bootstrapContainer, staticContainer!, scenePluginSettingsContainer, settings,
                    dynamicSettings, uiToolkitRoot, cursorRoot, splashScreen, backgroundMusic, worldInfoTool.EnsureNotNull(), playerEntity, destroyCancellationToken);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                await bootstrap.InitializeFeatureFlagsAsync(bootstrapContainer.IdentityCache!.Identity, bootstrapContainer.DecentralandUrlsSource, staticContainer!, ct);

                DisableInputs();

                if (await bootstrap.InitializePluginsAsync(staticContainer!, dynamicWorldContainer!, scenePluginSettingsContainer, globalPluginSettingsContainer, ct))
                {
                    GameReports.PrintIsDead();
                    return;
                }

                globalWorld = bootstrap.CreateGlobalWorld(bootstrapContainer, staticContainer!, dynamicWorldContainer!, debugUiRoot, playerEntity);

                staticContainer!.PlayerEntityProxy.SetObject(playerEntity);

                await bootstrap.LoadStartingRealmAsync(dynamicWorldContainer!, ct);
                await bootstrap.UserInitializationAsync(dynamicWorldContainer!, globalWorld, playerEntity, splashScreen, ct);

                RestoreInputs();
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception)
            {
                // unhandled exception
                GameReports.PrintIsDead();
                throw;
            }
        }

        private void DisableInputs()
        {
            // We disable Inputs directly because otherwise before login (so before the Input component was created and the system that handles it is working)
            // all inputs will be valid, and it allows for weird behaviour, including opening menus that are not ready to be open yet.
            staticContainer!.InputProxy.StrictObject.Shortcuts.Disable();
            staticContainer.InputProxy.StrictObject.Player.Disable();
            staticContainer.InputProxy.StrictObject.Emotes.Disable();
            staticContainer.InputProxy.StrictObject.EmoteWheel.Disable();
            staticContainer.InputProxy.StrictObject.FreeCamera.Disable();
            staticContainer.InputProxy.StrictObject.Camera.Disable();
        }

        private void RestoreInputs()
        {
            // We enable Inputs through the inputBlock so the block counters can be properly updated and the component Active flags are up-to-date as well
            // We restore all inputs except EmoteWheel and FreeCamera as they should be disabled by default
            staticContainer!.InputBlock.Enable(InputMapComponent.Kind.Shortcuts, InputMapComponent.Kind.Player, InputMapComponent.Kind.Emotes, InputMapComponent.Kind.Camera);
        }

        [ContextMenu(nameof(ValidateSettingsAsync))]
        public async UniTask ValidateSettingsAsync()
        {
            using var scope = new CheckingScope(ReportData.UNSPECIFIED);

            await UniTask.WhenAll(
                globalPluginSettingsContainer.EnsureValidAsync(),
                scenePluginSettingsContainer.EnsureValidAsync()
            );

            ReportHub.Log(ReportData.UNSPECIFIED, "Success checking");
        }

        private readonly struct CheckingScope : IDisposable
        {
            private readonly ReportData data;

            public CheckingScope(ReportData data)
            {
                this.data = data;
                ReportHub.Log(data, "Start checking");
            }

            public void Dispose()
            {
                ReportHub.Log(data, "Finish checking");
            }
        }
    }
}
