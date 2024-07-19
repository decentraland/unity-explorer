using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using SceneRunner.Debugging;
using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using System.Text.RegularExpressions;

namespace Global.Dynamic
{
    [Serializable]
    public class DebugSettings
    {
        public bool showSplash;
        public bool showAuthentication;
        public bool showLoading;
        public bool enableLandscape;
        public bool enableLOD;

        // To avoid configuration issues, force full flow on build (Debug.isDebugBuild is always true in Editor)
        public DebugSettings Get() =>
            Debug.isDebugBuild ? this : Release();

        public static DebugSettings Release() =>
            new ()
            {
                showSplash = true,
                showAuthentication = true,
                showLoading = true,
                enableLandscape = true,
                enableLOD = true,
            };
    }

    public class MainSceneLoader : MonoBehaviour
    {
        [Header("Startup Config")] [SerializeField]
        private RealmLaunchSettings launchSettings = null!;

        [Space]
        [SerializeField] private DebugViewsCatalog debugViewsCatalog = new ();

        [Space]
        [SerializeField] private DebugSettings debugSettings;

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
        private bool localSceneDevelopment = false;

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
            bootstrapContainer = await BootstrapContainer.CreateAsync(debugSettings, sceneLoaderSettings: settings, globalPluginSettingsContainer, destroyCancellationToken);

            IBootstrap bootstrap = bootstrapContainer!.Bootstrap;

            try
            {
                bootstrap.PreInitializeSetup(launchSettings, cursorRoot, debugUiRoot, splashRoot, destroyCancellationToken);

                bool isLoaded;
                (staticContainer, isLoaded) = await bootstrap.LoadStaticContainerAsync(bootstrapContainer, globalPluginSettingsContainer, debugViewsCatalog, ct);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                (dynamicWorldContainer, isLoaded) = await bootstrap.LoadDynamicWorldContainerAsync(bootstrapContainer, staticContainer!, scenePluginSettingsContainer, settings,
                    dynamicSettings, launchSettings, uiToolkitRoot, cursorRoot, splashScreenAnimation, backgroundMusic, worldInfoTool.EnsureNotNull(), destroyCancellationToken);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                await bootstrap.InitializeFeatureFlagsAsync(bootstrapContainer.IdentityCache.Identity!, staticContainer!, ct);

                if (await bootstrap.InitializePluginsAsync(staticContainer!, dynamicWorldContainer!, scenePluginSettingsContainer, globalPluginSettingsContainer, ct))
                {
                    GameReports.PrintIsDead();
                    return;
                }

                localSceneDevelopment = DetectLocalSceneDevelopment(); // we may need to inject this bool somewhere else...
                if (localSceneDevelopment)
                    dynamicWorldContainer.LocalSceneDevelopmentController.Initialize(launchSettings.GetStartingRealm());

                Entity playerEntity;
                (globalWorld, playerEntity) = bootstrap.CreateGlobalWorldAndPlayer(bootstrapContainer, staticContainer!, dynamicWorldContainer!, debugUiRoot);

                staticContainer!.PlayerEntityProxy.SetObject(playerEntity);

                await bootstrap.LoadStartingRealmAsync(dynamicWorldContainer!, ct);
                await bootstrap.UserInitializationAsync(dynamicWorldContainer!, globalWorld, playerEntity, splashScreenAnimation, splashRoot, ct);
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

        private bool DetectLocalSceneDevelopment()
        {
            // When started in preview mode (local scene development) a command line argument is used
            // Example (Windows) -> start decentraland://"realm=http://127.0.0.1:8000&position=100,100&otherparam=blahblah"

            // FOR DEBUGGING IN UNITY ONLY
            // string[] cmdArgs = new[] { "", "decentraland://realm=http://127.0.0.1:8000&position=100,100" };
            string[] cmdArgs = System.Environment.GetCommandLineArgs();

            if (cmdArgs.Length > 1)
            {
                // Regex to detect different parameters in Uri based on first param after '//' and then separated by '&'
                var pattern = @"(?<=://|&)[^?&]+=[^&]+";
                var regex = new Regex(pattern);
                var matches = regex.Matches(cmdArgs[1]);

                if (matches.Count == 0 || !matches[0].Value.Contains("realm=http://127.0.0.1:"))
                    return false;

                string localRealm = matches[0].Value.Replace("realm=", "");
                launchSettings.SetCustomStartingRealm(localRealm);

                if (matches.Count > 1)
                {
                    for (var i = 1; i < matches.Count; i++)
                    {
                        string param = matches[i].Value;

                        if (param.Contains("position="))
                        {
                            param = param.Replace("position=", "");

                            launchSettings.SetTargetScene(new Vector2Int(
                                int.Parse(param.Substring(0, param.IndexOf(','))),
                                int.Parse(param.Substring(param.IndexOf(',') + 1))));
                        }
                    }
                }

                return true;
            }

            return false;
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
