using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Input.Component;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using Global.AppArgs;
using LiveKit.Proto;
using PortableExperiences.Controller;
using SceneRunner.Debugging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public interface IDebugSettings
    {
        string[]? PortableExperiencesEnsToLoad { get; }
        string[]? EmotesToAddToUserProfile { get; }
        bool ShowSplash { get; }
        bool ShowAuthentication { get; }
        bool ShowLoading { get; }
        bool EnableLandscape { get; }
        bool EnableLOD { get; }
        bool EnableEmulateNoLivekitConnection { get; }
        bool OverrideConnectionQuality { get; }
        ConnectionQuality ConnectionQuality { get; }
        bool EnableRemotePortableExperiences { get; }
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
        [SerializeField] [Tooltip("Enable Portable Experiences obtained from Feature Flags from loading at the start of the game")]
        private bool enableRemotePortableExperiences;
        [SerializeField] [Tooltip("Make sure the ENS put here will be loaded as a GlobalPX (format must be something.dcl.eth)")]
        internal string[]? portableExperiencesEnsToLoad;
        [SerializeField]
        internal string[]? emotesToAddToUserProfile;
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
                portableExperiencesEnsToLoad = null,
                enableEmulateNoLivekitConnection = false,
                overrideConnectionQuality = false,
                connectionQuality = ConnectionQuality.QualityExcellent,
                enableRemotePortableExperiences = true,
                emotesToAddToUserProfile = null,
            };

        // To avoid configuration issues, force full flow on build (Debug.isDebugBuild is always true in Editor)
        public string[]? EmotesToAddToUserProfile => Debug.isDebugBuild ? this.emotesToAddToUserProfile : RELEASE_SETTINGS.emotesToAddToUserProfile;
        public string[]? PortableExperiencesEnsToLoad => Debug.isDebugBuild ? this.portableExperiencesEnsToLoad : RELEASE_SETTINGS.portableExperiencesEnsToLoad;
        public bool EnableRemotePortableExperiences => Debug.isDebugBuild ? this.enableRemotePortableExperiences : RELEASE_SETTINGS.enableRemotePortableExperiences;
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
                Entity playerEntity = world.Create(new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));
                (staticContainer, isLoaded) = await bootstrap.LoadStaticContainerAsync(bootstrapContainer, globalPluginSettingsContainer, debugViewsCatalog, playerEntity, ct);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                bootstrap.InitializePlayerEntity(staticContainer!, playerEntity);

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

                await bootstrap.LoadStartingRealmAsync(dynamicWorldContainer!, ct);

                await bootstrap.UserInitializationAsync(dynamicWorldContainer!, globalWorld, playerEntity, splashScreen, ct);

                LoadDebugPortableExperiences(ct);

                LoadRemotePortableExperiences(ct);

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

        private void LoadRemotePortableExperiences(CancellationToken ct)
        {
            if (!debugSettings.EnableRemotePortableExperiences) return;

            if (staticContainer!.FeatureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.GLOBAL_PORTABLE_EXPERIENCE, FeatureFlagsStrings.CSV_VARIANT))
            {
                if (!staticContainer.FeatureFlagsCache.Configuration.TryGetCsvPayload(FeatureFlagsStrings.GLOBAL_PORTABLE_EXPERIENCE, "csv-variant", out List<List<string>>? csv)) return;

                if (csv?[0] == null) return;

                foreach (string value in csv[0]) { staticContainer.PortableExperiencesController!.
                                                                   CreatePortableExperienceByEnsAsync(new ENS(value), ct, true, true).
                                                                   SuppressAnyExceptionWithFallback(new IPortableExperiencesController.SpawnResponse(), ReportCategory.PORTABLE_EXPERIENCE).Forget(); }
            }
        }

        private void LoadDebugPortableExperiences(CancellationToken ct)
        {
            if (debugSettings.portableExperiencesEnsToLoad == null) return;

            foreach (string pxEns in debugSettings.portableExperiencesEnsToLoad)
            {
                staticContainer!.PortableExperiencesController.
                                 CreatePortableExperienceByEnsAsync(new ENS(pxEns), ct, true, true).
                                 SuppressAnyExceptionWithFallback(new IPortableExperiencesController.SpawnResponse(), ReportCategory.PORTABLE_EXPERIENCE).Forget();
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
            staticContainer!.InputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.PLAYER, InputMapComponent.Kind.EMOTES, InputMapComponent.Kind.CAMERA);
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
