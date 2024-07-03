using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Identities;
using Segment.Serialization;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using static DCL.PerformanceAndDiagnostics.Analytics.AnalyticsEvents;

namespace Global.Dynamic
{
    public class BootstrapAnalyticsDecorator : IBootstrap
    {
        private const string STAGE_KEY = "state";
        private const string RESULT_KEY = "result";

        private readonly Bootstrap core;
        private readonly AnalyticsConfiguration analyticsConfig;

        private AnalyticsController analytics;

        public BootstrapAnalyticsDecorator(Bootstrap core, AnalyticsConfiguration analyticsConfig)
        {
            this.core = core;
            this.analyticsConfig = analyticsConfig;
        }

        public void Dispose()
        {
            core.Dispose();
        }

        public UniTask PreInitializeSetup(RealmLaunchSettings launchSettings, UIDocument cursorRoot, UIDocument debugUiRoot, GameObject splashRoot, DebugViewsCatalog debugViewsCatalog,
            CancellationToken ct)
        {
            IAnalyticsService service = analyticsConfig.Mode switch
                                        {
                                            AnalyticsMode.SEGMENT => new SegmentAnalyticsService(analyticsConfig),
                                            AnalyticsMode.DEBUG_LOG => new DebugAnalyticsService(),
                                            _ => throw new ArgumentOutOfRangeException(),
                                        };

            analytics = new AnalyticsController(service, analyticsConfig);

            analytics.Track(General.SYSTEM_INFO_REPORT, new JsonObject
            {
                ["device_model"] = SystemInfo.deviceModel, // "XPS 17 9720 (Dell Inc.)"
                ["operating_system"] = SystemInfo.operatingSystem, // "Windows 11  (10.0.22631) 64bit"
                ["system_memory_size"] = SystemInfo.systemMemorySize, // 65220 in [MB]

                ["processor_type"] = SystemInfo.processorType, // "12th Gen Intel(R) Core(TM) i7-12700H"
                ["processor_count"] = SystemInfo.processorCount, // 20

                ["graphics_device_name"] = SystemInfo.graphicsDeviceName, // "NVIDIA GeForce RTX 3050 Laptop GPU"
                ["graphics_memory_size"] = SystemInfo.graphicsMemorySize, // 3965 in [MB]
                ["graphics_device_type"] = SystemInfo.graphicsDeviceType.ToString(), // "Direct3D11", Vulkan, OpenGLCore, XBoxOne...
                ["graphics_device_version"] = SystemInfo.graphicsDeviceVersion, // "Direct3D 11.0 [level 11.1]"
            });

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "started" },
            });

            return core.PreInitializeSetup(launchSettings, cursorRoot, debugUiRoot, splashRoot, debugViewsCatalog, ct);
        }

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(BootstrapContainer bootstrapContainer, PluginSettingsContainer globalPluginSettingsContainer, CancellationToken ct)
        {
            (StaticContainer? container, bool isSuccess) result = await core.LoadStaticContainerAsync(bootstrapContainer, globalPluginSettingsContainer, ct);

            analytics.SetCommonParam(result.container!.RealmData, bootstrapContainer.IdentityCache, result.container.CharacterContainer.Transform);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "static container loaded" },
                { RESULT_KEY, result.isSuccess ? "success" : "failure" },
            });

            return result;
        }

        public async UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(BootstrapContainer bootstrapContainer, StaticContainer staticContainer, PluginSettingsContainer scenePluginSettingsContainer, DynamicSceneLoaderSettings settings, DynamicSettings dynamicSettings,
            RealmLaunchSettings launchSettings, UIDocument uiToolkitRoot, UIDocument cursorRoot, Animator splashScreenAnimation, AudioClipConfig backgroundMusic,
            CancellationToken ct)
        {
            (DynamicWorldContainer? container, bool) result =
                await core.LoadDynamicWorldContainerAsync(bootstrapContainer, staticContainer, scenePluginSettingsContainer,
                    settings, dynamicSettings, launchSettings, uiToolkitRoot, cursorRoot, splashScreenAnimation, backgroundMusic, ct);

            result.container.GlobalPlugins.Add
            (
                new AnalyticsPlugin(
                    analytics,
                    analyticsConfig,
                    staticContainer.ProfilingProvider,
                    staticContainer.RealmData,
                    staticContainer.ScenesCache,
                    result.container.MvcManager,
                    result.container.ChatMessagesBus,
                    result.container.GoToChatCommand)
            );

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "dynamic container loaded" },
                { RESULT_KEY, result.Item2 ? "success" : "failure" },
            });

            return result;
        }

        public async UniTask<bool> InitializePluginsAsync(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer, PluginSettingsContainer scenePluginSettingsContainer, PluginSettingsContainer globalPluginSettingsContainer, CancellationToken ct)
        {
            bool anyFailure = await core.InitializePluginsAsync(staticContainer, dynamicWorldContainer, scenePluginSettingsContainer, globalPluginSettingsContainer, ct);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "plugins initialized" },
                { RESULT_KEY, !anyFailure ? "success" : "failure" },
            });

            return anyFailure;
        }

        public UniTask InitializeFeatureFlagsAsync(IWeb3Identity identity, StaticContainer staticContainer, CancellationToken ct)
        {
            UniTask result = core.InitializeFeatureFlagsAsync(identity, staticContainer, ct);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "feature flag initialized" },
            });

            return result;
        }

        public (GlobalWorld, Entity) CreateGlobalWorldAndPlayer(BootstrapContainer bootstrapContainer, StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer, UIDocument debugUiRoot)
        {
            (GlobalWorld, Entity) result = core.CreateGlobalWorldAndPlayer(bootstrapContainer, staticContainer, dynamicWorldContainer, debugUiRoot);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "global world and player created" },
            });

            return result;
        }

        public async UniTask LoadStartingRealmAsync(DynamicWorldContainer dynamicWorldContainer, CancellationToken ct)
        {
            await core.LoadStartingRealmAsync(dynamicWorldContainer, ct);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "realm loaded" },
            });
        }

        public async UniTask UserInitializationAsync(DynamicWorldContainer dynamicWorldContainer, GlobalWorld? globalWorld, Entity playerEntity, Animator splashScreenAnimation, GameObject splashRoot,
            CancellationToken ct)
        {
            dynamicWorldContainer.RealFlowLoadingStatus.StageChanged += OnLoadingStageChanged;
            await core.UserInitializationAsync(dynamicWorldContainer, globalWorld, playerEntity, splashScreenAnimation, splashRoot, ct);
            dynamicWorldContainer.RealFlowLoadingStatus.StageChanged -= OnLoadingStageChanged;

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "end" },
            });
        }

        private void OnLoadingStageChanged(RealFlowLoadingStatus.Stage stage)
        {
            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, $"loading screen: {stage}" },
            });
        }
    }
}
