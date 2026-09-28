using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Browser.DecentralandUrls;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Ipfs;
using DCL.Landscape;
using DCL.LOD.Systems;
using DCL.MapRenderer.ComponentsFactory;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.RealmNavigation;
using DCL.SkyBox;
using DCL.Time;
using DCL.Utilities;
using DCL.Utility;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.ChromeDevtool;
using ECS;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using Global.Dynamic;
using Global.Dynamic.Landscapes;
using Global.Versioning;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Global.MapCapture
{
    /// <summary>
    ///     Boots only what a map capture renders with: asset bundle loading, ISS LOD_0 assembly, roads, terrain,
    ///     skybox and the client's camera rig. No login, comms, UI, avatar or scene runtime.
    /// </summary>
    public static class MapCaptureBootstrap
    {
        private const string QUALITY_LEVEL = "High";
        private const string GENESIS_INSTALL_SOURCE = "";

        public static async UniTask<MapCaptureRuntime> CreateAsync(IAppArgs appArgs, PluginSettingsContainer settingsContainer, Light directionalLight,
            DecentralandEnvironment environment, MonoBehaviour coroutineRunner, CancellationToken ct)
        {
            // Offline flags: only the switches the capture depends on, so a run never depends on the flags service.
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(CaptureFlags()));
            FeaturesRegistry.Initialize(new FeaturesRegistry(appArgs, false));

            var realmData = new RealmData();
            IDecentralandUrlsSource urls = new DecentralandUrlsSource(environment, realmData, ILaunchMode.PLAY, abgenPipelineForced: true, abgenLodsForced: true);
            IAssetsProvisioner assetsProvisioner = new AddressablesProvisioner().WithErrorTrace();
            IDebugContainerBuilder debugBuilder = new NullDebugContainerBuilder();
            IWeb3IdentityCache identityCache = new MemoryWeb3IdentityCache();
            IReportsHandlingSettings reportSettings = ReportSettings(settingsContainer);
            DiagnosticsContainer diagnostics = DiagnosticsContainer.Create(reportSettings, false);

            World world = World.Create();
            Entity playerEntity = world.Create();

            AnalyticsContainer analytics = await AnalyticsContainer.CreateAsync(appArgs, identityCache, ILaunchMode.PLAY, debugBuilder, GENESIS_INSTALL_SOURCE, settingsContainer, DCLVersion.FromAppArgs(appArgs), ct);
            WebRequestsContainer webRequests = await WebRequestsContainer.CreateAsync(settingsContainer, identityCache, debugBuilder, urls, ChromeDevToolHandler.New(false), null, new RealmClock(), ct);

            (StaticContainer? staticContainer, bool created) = await StaticContainer.CreateAsync(
                analytics, urls, realmData, assetsProvisioner, reportSettings, debugBuilder, webRequests, settingsContainer, diagnostics,
                identityCache, new NoopEthereumApi(), ILaunchMode.PLAY, useRemoteAssetBundles: true, useLocalAssetBundles: false,
                world, playerEntity, new SystemMemoryCap(), new VolumeBus(), enableAnalytics: false,
                new IDiskCache.Fake(), IDiskCache<PartialLoadingState>.Null.INSTANCE, ct, appArgs);

            if (!created || staticContainer == null)
                throw new InvalidOperationException("Cannot create the static container");

            // Scenes stream by budget relative to the player; the feeder pins every request to top priority, but the
            // deferred loader still reads the player's transform.
            staticContainer.CharacterContainer.InitializePlayerEntity(world, playerEntity);
            staticContainer.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed);
            ApplyQualityLevel(staticContainer);

            (DefaultTexturesContainer? textures, bool texturesCreated) = await DefaultTexturesContainer.CreateAsync(settingsContainer, assetsProvisioner, appArgs, ct);

            if (!texturesCreated || textures == null)
                throw new InvalidOperationException("Cannot create the default textures container");

            (LODContainer? lodContainer, bool lodCreated) = await LODContainer.CreateAsync(assetsProvisioner, staticContainer, settingsContainer, realmData,
                textures.TextureArrayContainerFactory, debugBuilder, true, staticContainer.GPUInstancingService, urls, ct);

            if (!lodCreated || lodContainer == null)
                throw new InvalidOperationException("Cannot create the LOD container");

            var genesisTerrain = new TerrainGenerator(staticContainer.Profiler);
            var worldsTerrain = new WorldTerrainGenerator();
            var landscape = new Landscape(new MapCaptureRealmController(realmData), genesisTerrain, worldsTerrain, true);
            var landscapePlugin = new LandscapePlugin(realmData, genesisTerrain, worldsTerrain, assetsProvisioner, debugBuilder, new MapRendererTextureContainer(), true, landscape);
            var skyboxPlugin = new SkyboxPlugin(assetsProvisioner, directionalLight, staticContainer.ScenesCache, staticContainer.SceneRestrictionBusController, realmData);

            if (staticContainer.GPUInstancingService != null)
                staticContainer.GPUInstancingService.LandscapeData = staticContainer.QualityContainer.LandscapeData;

            var plugins = new List<IDCLGlobalPlugin>(staticContainer.SharedPlugins)
            {
                lodContainer.LODPlugin,
                lodContainer.RoadPlugin,
                landscapePlugin,
                skyboxPlugin,
                staticContainer.QualityContainer.CreatePlugin(),
            };

            foreach (IDCLGlobalPlugin plugin in plugins)
            {
                (_, bool initialized) = await settingsContainer.InitializePluginAsync(plugin, ct);

                if (!initialized)
                    throw new InvalidOperationException($"Cannot initialize {plugin.GetType().Name}");
            }

            MapCaptureCamera camera = await MapCaptureCamera.CreateAsync(settingsContainer, assetsProvisioner, world, coroutineRunner, ct);
            var feeder = new MapCaptureSceneFeeder(urls, staticContainer.ScenesCache, realmData, lodContainer.RoadCoordinates, staticContainer.RealmPartitionSettings.ScenesDefinitionsRequestBatchSize);
            SystemGroupWorld systems = MapCaptureWorldFactory.Create(world, staticContainer, urls, realmData, analytics.EntitiesAnalytics, lodContainer, plugins, playerEntity, feeder, camera.Camera);

            await MapCaptureRealm.ConfigureGenesisAsync(realmData, staticContainer.WebRequestsContainer.WebRequestController, urls, staticContainer.WorldManifestProvider, environment, ct);

            var terrain = await landscape.LoadTerrainAsync(AsyncLoadProcessReport.Create(ct), ct);

            if (!terrain.Success)
                throw new InvalidOperationException($"Terrain generation failed: {terrain.Error?.Message}");

            return new MapCaptureRuntime(world, systems, staticContainer, realmData, feeder, camera, staticContainer.StaticSettings.SkyboxSettings);
        }

        private static FeatureFlagsResultDto CaptureFlags() =>
            new ()
            {
                flags = new Dictionary<string, bool>
                {
                    [FeatureFlagsStrings.NEW_LODS] = true,
                    [FeatureFlagsStrings.ASSET_BUNDLE_FALLBACK] = true,
                    [FeatureFlagsStrings.ABGEN_PIPELINE] = true,
                    [FeatureFlagsStrings.ABGEN_LODS] = true,
                },
                variants = new Dictionary<string, FeatureFlagVariantDto>(),
            };

        private static IReportsHandlingSettings ReportSettings(IPluginSettingsContainer settingsContainer)
        {
            BootstrapSettings settings = settingsContainer.GetSettings<BootstrapSettings>();
#if (DEVELOPMENT_BUILD || UNITY_EDITOR) && !ENABLE_PROFILING
            return settings.ReportHandlingSettingsDevelopment;
#else
            return settings.ReportHandlingSettingsProduction;
#endif
        }

        private static void ApplyQualityLevel(StaticContainer staticContainer)
        {
            int index = Array.IndexOf(QualitySettings.names, QUALITY_LEVEL);
            staticContainer.QualityContainer.QualityLevelController.SetLevel(Mathf.Max(index, 0));
        }
    }

    /// <summary>Everything the capture job drives, owned for the lifetime of the scene.</summary>
    public class MapCaptureRuntime : IDisposable
    {
        public readonly World World;
        public readonly StaticContainer StaticContainer;
        public readonly RealmData RealmData;
        public readonly MapCaptureSceneFeeder Feeder;
        public readonly MapCaptureCamera Camera;
        public readonly SkyboxSettingsAsset SkyboxSettings;

        private readonly SystemGroupWorld systems;

        public MapCaptureRuntime(World world, SystemGroupWorld systems, StaticContainer staticContainer, RealmData realmData, MapCaptureSceneFeeder feeder,
            MapCaptureCamera camera, SkyboxSettingsAsset skyboxSettings)
        {
            World = world;
            this.systems = systems;
            StaticContainer = staticContainer;
            RealmData = realmData;
            Feeder = feeder;
            Camera = camera;
            SkyboxSettings = skyboxSettings;
        }

        public void Dispose()
        {
            systems.Dispose();
            Camera.Dispose();
            StaticContainer.Dispose();
        }
    }

    /// <summary>The capture has no wallet; nothing in its boot path sends Ethereum requests.</summary>
    internal class NoopEthereumApi : IEthereumApi
    {
        public UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct) =>
            throw new NotSupportedException("The map capture has no wallet");

        public void Dispose() { }
    }

    /// <summary>The landscape only reads the realm data through its controller.</summary>
    internal class MapCaptureRealmController : IGlobalRealmController
    {
        public IRealmData RealmData { get; }
        public URLDomain? CurrentDomain => null;
        public GlobalWorld GlobalWorld { get; set; } = null!;

        public MapCaptureRealmController(IRealmData realmData)
        {
            RealmData = realmData;
        }

        public UniTask SetRealmAsync(URLDomain realm, CancellationToken ct) =>
            throw new NotSupportedException("The map capture realm is fixed to Genesis City");

        public UniTask<bool> IsReachableAsync(URLDomain realm, CancellationToken ct) =>
            UniTask.FromResult(false);

        public void DisposeGlobalWorld() { }

        public UniTask<List<SceneEntityDefinition>> WaitForFixedScenePromisesAsync(CancellationToken ct) =>
            UniTask.FromResult(new List<SceneEntityDefinition>());

        public UniTask<SceneDefinitions?> WaitForStaticScenesEntityDefinitionsAsync(CancellationToken ct) =>
            UniTask.FromResult<SceneDefinitions?>(null);
    }
}
