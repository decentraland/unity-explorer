using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AssetsProvision.CodeResolver;
using DCL.Audio;
using DCL.Browser.DecentralandUrls;
using DCL.CharacterMotion.Components;
using DCL.Clipboard;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Ipfs;
using DCL.Landscape;
using DCL.LOD.Systems;
using DCL.MapRenderer.ComponentsFactory;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.Prefs;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.Profiles;
using DCL.RealmNavigation;
using DCL.SkyBox;
using DCL.Time;
using DCL.Utilities;
using DCL.Utility;
using DCL.Utility.Types;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.ChromeDevtool;
using ECS;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using Global.Dynamic;
using Global.Dynamic.Landscapes;
using Global.Versioning;
using MVC;
using SceneRuntime.Factory.WebSceneSource;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Global.MapCapture
{
    /// <summary>
    ///     Boots only what a map capture renders with: asset bundle loading, ISS LOD_0 assembly, roads, terrain,
    ///     skybox and the client's camera rig. No login, comms, UI or avatar. The scene runtime is created only for
    ///     a live scene capture.
    /// </summary>
    public static class MapCaptureBootstrap
    {
        private const string QUALITY_LEVEL = "High";
        private const string GENESIS_INSTALL_SOURCE = "";
        private const string LENS_FLARE_COMPONENT = "LensFlareComponentSRP";

        public static async UniTask<MapCaptureRuntime> CreateAsync(IAppArgs appArgs, MapCaptureArgs args, PluginSettingsContainer settingsContainer, Light directionalLight,
            DecentralandEnvironment environment, MonoBehaviour coroutineRunner, CancellationToken ct)
        {
            if (args.CacheDir != null)
                RedirectBundleCache(args.CacheDir);

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

            // Scene physics systems read and clear this on the player even when the scene is not current.
            world.Add(playerEntity, new CharacterRigidTransform());
            staticContainer.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed);
            ApplyQualityLevel(staticContainer);

            (DefaultTexturesContainer? textures, bool texturesCreated) = await DefaultTexturesContainer.CreateAsync(settingsContainer, assetsProvisioner, appArgs, ct);

            if (!texturesCreated || textures == null)
                throw new InvalidOperationException("Cannot create the default textures container");

            (LODContainer? lodContainer, bool lodCreated) = await LODContainer.CreateAsync(assetsProvisioner, staticContainer, settingsContainer, realmData,
                textures.TextureArrayContainerFactory, debugBuilder, true, staticContainer.GPUInstancingService, urls, ct);

            if (!lodCreated || lodContainer == null)
                throw new InvalidOperationException("Cannot create the LOD container");

            var feeder = new MapCaptureSceneFeeder(urls, staticContainer.ScenesCache, realmData, lodContainer.RoadCoordinates, staticContainer.RealmPartitionSettings.ScenesDefinitionsRequestBatchSize);
            var genesisTerrain = new TerrainGenerator(staticContainer.Profiler);
            var worldsTerrain = new WorldTerrainGenerator();
            var landscape = new Landscape(new MapCaptureRealmController(realmData, feeder), genesisTerrain, worldsTerrain, true);
            var landscapePlugin = new LandscapePlugin(realmData, genesisTerrain, worldsTerrain, assetsProvisioner, debugBuilder, new MapRendererTextureContainer(), true, landscape);
            // A straight-down camera never faces the sun, but the sun's lens flare still draws; the skybox reads this
            // preference when it sets the flare up, and the component is disabled again below in case anything re-enables it.
            DCLPlayerPrefs.SetBool(DCLPrefKeys.PS_SUN_LENS_FLARE, false);
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

            // In debug builds the scene world's light source debug system reads the state entity this plugin creates.
            if (args.SceneParcel.HasValue)
                plugins.Add(new LightSourceDebugPlugin(debugBuilder, world));

            foreach (IDCLGlobalPlugin plugin in plugins)
            {
                (_, bool initialized) = await settingsContainer.InitializePluginAsync(plugin, ct);

                if (!initialized)
                    throw new InvalidOperationException($"Cannot initialize {plugin.GetType().Name}");
            }

            // Looked up by name: the SRP core assembly that declares LensFlareComponentSRP is not referenced here.
            if (directionalLight.GetComponent(LENS_FLARE_COMPONENT) is Behaviour lensFlare)
                lensFlare.enabled = false;

            MapCaptureCamera camera = await MapCaptureCamera.CreateAsync(settingsContainer, assetsProvisioner, world, coroutineRunner, args.KeepBloom, ct);
            SystemGroupWorld systems = MapCaptureWorldFactory.Create(world, staticContainer, urls, realmData, analytics.EntitiesAnalytics, lodContainer, plugins, playerEntity, feeder, camera.Camera);

            // Worlds configure the realm and generate their terrain one world at a time; Genesis City is never loaded.
            if (!args.CapturesWorlds)
            {
                await MapCaptureRealm.ConfigureGenesisAsync(realmData, staticContainer.WebRequestsContainer.WebRequestController, urls, staticContainer.WorldManifestProvider, environment, ct);
                await LoadTerrainAsync(landscape, ct);
            }

            MapCaptureLiveSceneLoader? liveScenes = args.SceneParcel.HasValue
                ? await CreateLiveSceneLoaderAsync(staticContainer, settingsContainer, urls, realmData, identityCache, environment, world, camera.CameraEntity, ct)
                : null;

            return new MapCaptureRuntime(world, systems, staticContainer, realmData, urls, environment, landscape, feeder, camera, staticContainer.StaticSettings.SkyboxSettings, liveScenes);
        }

        /// <summary>The client's terrain for the configured realm: Genesis City's, or the current world's around its parcels.</summary>
        public static async UniTask LoadTerrainAsync(Landscape landscape, CancellationToken ct)
        {
            EnumResult<LandscapeError> terrain = await landscape.LoadTerrainAsync(AsyncLoadProcessReport.Create(ct), ct);

            if (!terrain.Success)
                throw new InvalidOperationException($"Terrain generation failed: {terrain.Error?.Message}");
        }

        /// <summary>
        ///     The scene half of the client: the per-scene world plugins and the scene factory, with no comms, profiles
        ///     or UI behind the APIs a scene can call. Mirrors the play mode integration test suite.
        /// </summary>
        private static async UniTask<MapCaptureLiveSceneLoader> CreateLiveSceneLoaderAsync(StaticContainer staticContainer, PluginSettingsContainer settingsContainer,
            IDecentralandUrlsSource urls, RealmData realmData, IWeb3IdentityCache identityCache, DecentralandEnvironment environment, World world, Entity cameraEntity, CancellationToken ct)
        {
            foreach (IDCLWorldPlugin plugin in staticContainer.ECSWorldPlugins)
            {
                (_, bool initialized) = await settingsContainer.InitializePluginAsync(plugin, ct);

                if (!initialized)
                    throw new InvalidOperationException($"Cannot initialize {plugin.GetType().Name}");
            }

            // Scene systems reach the camera through this proxy; the character camera plugin that normally sets it is not booted.
            staticContainer.ExposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy.SetObject(cameraEntity);

            IWebRequestController webRequests = staticContainer.WebRequestsContainer.WebRequestController;
            var mvcManager = new MVCManager(new WindowStackManager(), new CancellationTokenSource(), new MapCapturePopupCloserView());

            SceneSharedContainer sceneShared = SceneSharedContainer.Create(in staticContainer, urls, identityCache, webRequests, realmData,
                new MemoryProfileRepository(new DefaultProfileCache()), NullRoomHub.INSTANCE, mvcManager, new IMessagePipesHub.Fake(), new MapCaptureRemoteMetadata(),
                new WebJsSources(new JsCodeResolver(webRequests)), environment, new UnityClipboard(), Array.Empty<IDCLWorldPlugin>());

            sceneShared.SceneFactory.SetGlobalWorldActions(new MapCaptureWorldActions());

            var loadLogic = new LoadSceneSystemLogic(webRequests, URLDomain.FromString(urls.Url(DecentralandUrl.AssetBundlesCDN)));
            return new MapCaptureLiveSceneLoader(world, urls, sceneShared.SceneFactory, loadLogic, staticContainer.SceneReadinessReportQueue);
        }

        /// <summary>
        ///     Bundles download through Unity's own cache. Its default lives on the system drive and is size-capped, so
        ///     a whole-city run points it at a folder of its own with no cap. Every cache stays readable; only writes move.
        /// </summary>
        private static void RedirectBundleCache(string directory)
        {
            Directory.CreateDirectory(directory);
            Cache cache = Caching.AddCache(directory);

            if (!cache.valid)
                throw new InvalidOperationException($"Cannot use {directory} as the asset bundle cache");

            cache.maximumAvailableStorageSpace = long.MaxValue;
            Caching.currentCacheForWriting = cache;
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
        public readonly IDecentralandUrlsSource Urls;
        public readonly DecentralandEnvironment Environment;
        public readonly Landscape Landscape;
        public readonly MapCaptureSceneFeeder Feeder;
        public readonly MapCaptureCamera Camera;
        public readonly SkyboxSettingsAsset SkyboxSettings;

        /// <summary>Present only when the run captures a live scene.</summary>
        public readonly MapCaptureLiveSceneLoader? LiveScenes;

        private readonly SystemGroupWorld systems;

        public MapCaptureRuntime(World world, SystemGroupWorld systems, StaticContainer staticContainer, RealmData realmData, IDecentralandUrlsSource urls,
            DecentralandEnvironment environment, Landscape landscape, MapCaptureSceneFeeder feeder, MapCaptureCamera camera, SkyboxSettingsAsset skyboxSettings,
            MapCaptureLiveSceneLoader? liveScenes)
        {
            World = world;
            this.systems = systems;
            StaticContainer = staticContainer;
            RealmData = realmData;
            Urls = urls;
            Environment = environment;
            Landscape = landscape;
            Feeder = feeder;
            Camera = camera;
            SkyboxSettings = skyboxSettings;
            LiveScenes = liveScenes;
        }

        public void Dispose()
        {
            LiveScenes?.Dispose();
            systems.Dispose();
            Camera.Dispose();
            StaticContainer.Dispose();
        }
    }

    /// <summary>The capture has no wallet; a scene's wallet call is rejected rather than answered.</summary>
    internal class NoopEthereumApi : IEthereumApi
    {
        public UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct) =>
            throw new NotSupportedException("The map capture has no wallet");

        public void Dispose() { }
    }

    /// <summary>
    ///     The landscape reads the realm data and, in a world, the world's scene definitions through its controller. The
    ///     realm itself is configured by <see cref="MapCaptureRealm" />, not through this controller.
    /// </summary>
    internal class MapCaptureRealmController : IGlobalRealmController
    {
        private readonly MapCaptureSceneFeeder feeder;

        public IRealmData RealmData { get; }
        public URLDomain? CurrentDomain => null;
        public GlobalWorld GlobalWorld { get; set; } = null!;

        public MapCaptureRealmController(IRealmData realmData, MapCaptureSceneFeeder feeder)
        {
            RealmData = realmData;
            this.feeder = feeder;
        }

        public UniTask SetRealmAsync(URLDomain realm, CancellationToken ct) =>
            throw new NotSupportedException("The map capture configures its realm through MapCaptureRealm");

        public UniTask<bool> IsReachableAsync(URLDomain realm, CancellationToken ct) =>
            UniTask.FromResult(false);

        public void DisposeGlobalWorld() { }

        public async UniTask<List<SceneEntityDefinition>> WaitForFixedScenePromisesAsync(CancellationToken ct)
        {
            await UniTask.WaitUntil(() => feeder.WorldDefinitionsResolved, cancellationToken: ct);
            return new List<SceneEntityDefinition>(feeder.WorldDefinitions);
        }

        public UniTask<SceneDefinitions?> WaitForStaticScenesEntityDefinitionsAsync(CancellationToken ct) =>
            UniTask.FromResult<SceneDefinitions?>(null);
    }
}
