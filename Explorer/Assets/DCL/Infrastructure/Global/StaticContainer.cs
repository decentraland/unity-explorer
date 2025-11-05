using Arch.Core;
using CommunicationData.URLHelpers;
using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.Character.Plugin;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.FeatureFlags;
using DCL.Gizmos.Plugin;
using DCL.Input;
using DCL.Interaction.Utility;
using DCL.Landscape.Parcel;
using DCL.Landscape.Utils;
using DCL.MapPins.Bus;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PerformanceAndDiagnostics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiling;
using DCL.Quality;
using DCL.ResourcesUnloading;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using ECS;
using ECS.Prioritization;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Reporting;
using SceneRunner.Mapping;
using System.Collections.Generic;
using System.Threading;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles;
using DCL.RealmNavigation;
using DCL.Rendering.GPUInstancing;
using DCL.SDKComponents.MediaStream;
using DCL.SDKComponents.SkyboxTime;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;
using Global.Dynamic.LaunchModes;
using PortableExperiences.Controller;
using System.Buffers;
using UnityEngine;
using Utility;
using MultiplayerPlugin = DCL.PluginSystem.World.MultiplayerPlugin;

namespace Global
{
    /// <summary>
    ///     Produces dependencies that never change during the lifetime of the application
    ///     and are not connected to the global world or scenes but are used by them.
    ///     This is the first container to instantiate, should not depend on any other container
    /// </summary>
    public class StaticContainer : IDCLPlugin<StaticSettings>
    {
        public readonly ObjectProxy<AvatarBase> MainPlayerAvatarBaseProxy = new ();
        public readonly ObjectProxy<IRoomHub> RoomHubProxy = new ();
        public readonly ObjectProxy<IReadOnlyEntityParticipantTable> EntityParticipantTableProxy = new ();
        public readonly RealmData RealmData = new ();
        public readonly PartitionDataContainer PartitionDataContainer = new ();
        public readonly IMapPinsEventBus MapPinsEventBus = new MapPinsEventBus();
        public readonly LandscapeParcelData LandscapeParcelData = new ();

        private IAssetsProvisioner assetsProvisioner;
        public Entity PlayerEntity { get; set; }

        public ComponentsContainer ComponentsContainer { get; private set; }
        public CharacterContainer CharacterContainer { get; private set; }
        public MediaPlayerContainer MediaContainer { get; private set; }
        public QualityContainer QualityContainer { get; private set; }
        public ExposedGlobalDataContainer ExposedGlobalDataContainer { get; private set; }
        public WebRequestsContainer WebRequestsContainer { get; private set; }
        public IReadOnlyList<IDCLWorldPlugin> ECSWorldPlugins { get; private set; }

        public ISystemMemoryCap MemoryCap { get; private set; }

        public SceneLoadingLimit SceneLoadingLimit { get; private set; }

        /// <summary>
        ///     Some plugins may implement both interfaces
        /// </summary>
        public IReadOnlyList<IDCLGlobalPlugin> SharedPlugins { get; private set; }
        public ECSWorldSingletonSharedDependencies SingletonSharedDependencies { get; private set; }
        public Profiler Profiler { get; private set; }
        public IEntityCollidersGlobalCache EntityCollidersGlobalCache { get; private set; }
        public IPartitionSettings PartitionSettings => StaticSettings.PartitionSettings;
        public IRealmPartitionSettings RealmPartitionSettings => StaticSettings.RealmPartitionSettings;
        public StaticSettings StaticSettings { get; private set; }
        public CacheCleaner CacheCleaner { get; private set; }
        public IEthereumApi EthereumApi { get; private set; }
        public IInputBlock InputBlock { get; private set; }
        public IScenesCache ScenesCache { get; private set; }
        public ISceneReadinessReportQueue SceneReadinessReportQueue { get; private set; }
        public HttpFeatureFlagsProvider FeatureFlagsProvider { get; private set; }
        public IPortableExperiencesController PortableExperiencesController { get; private set; }
        public IDebugContainerBuilder DebugContainerBuilder { get; private set; }
        public ISceneRestrictionBusController SceneRestrictionBusController { get; private set; }
        public GPUInstancingService GPUInstancingService { get; private set; }
        public ILoadingStatus LoadingStatus { get; private set; }
        public ILaunchMode LaunchMode { get; private set; }
        public LandscapeParcelController LandscapeParcelController { get; private set; }

        public void Dispose()
        {
            QualityContainer.Dispose();
            Profiler.Dispose();
            SceneRestrictionBusController.Dispose();
        }

        public UniTask InitializeAsync(StaticSettings settings, CancellationToken ct)
        {
            StaticSettings = settings;
            return UniTask.CompletedTask;
        }

        public static async UniTask<(StaticContainer? container, bool success)> CreateAsync(
            IDecentralandUrlsSource decentralandUrlsSource,
            IAssetsProvisioner assetsProvisioner,
            IReportsHandlingSettings reportHandlingSettings,
            IDebugContainerBuilder debugContainerBuilder,
            WebRequestsContainer webRequestsContainer,
            IPluginSettingsContainer settingsContainer,
            DiagnosticsContainer diagnosticsContainer,
            IWeb3IdentityCache web3IdentityProvider,
            IEthereumApi ethereumApi,
            ILaunchMode launchMode,
            bool useRemoteAssetBundles,
            World globalWorld,
            Entity playerEntity,
            ISystemMemoryCap memoryCap,
            VolumeBus volumeBus,
            bool enableAnalytics,
            IAnalyticsController analyticsController,
            IDiskCache diskCache,
            IDiskCache<PartialLoadingState> partialsDiskCache,
            ObjectProxy<IProfileRepository> profileRepository,
            DecentralandEnvironment environment,
            CancellationToken ct,
            bool hasDebugFlag,
            bool enableGPUInstancing = true)
        {
            ProfilingCounters.CleanAllCounters();
            SentryTransactionManager.Initialize(new SentryTransactionManager());

            var componentsContainer = ComponentsContainer.Create();
            var exposedGlobalDataContainer = ExposedGlobalDataContainer.Create();
            var profilingProvider = new Profiler();
            var container = new StaticContainer();

            container.PlayerEntity = playerEntity;
            container.DebugContainerBuilder = debugContainerBuilder;
            container.EthereumApi = ethereumApi;
            container.ScenesCache = new ScenesCache();
            container.SceneReadinessReportQueue = new SceneReadinessReportQueue(container.ScenesCache);
            container.InputBlock = new ECSInputBlock(globalWorld);
            container.assetsProvisioner = assetsProvisioner;
            container.MemoryCap = memoryCap;
            container.SceneRestrictionBusController = new SceneRestrictionBusController();
            container.LaunchMode = launchMode;

            var exposedPlayerTransform = new ExposedTransform();

            StaticSettings staticSettings = settingsContainer.GetSettings<StaticSettings>();

            container.LoadingStatus = enableAnalytics ? new LoadingStatusAnalyticsDecorator(new LoadingStatus(), analyticsController, web3IdentityProvider) : new LoadingStatus();

            var sharedDependencies = new ECSWorldSingletonSharedDependencies(
                componentsContainer.ComponentPoolsRegistry,
                reportHandlingSettings,
                new SceneEntityFactory(),
                new PartitionedWorldsAggregate.Factory(),
                new ConcurrentLoadingPerformanceBudget(staticSettings.AssetsLoadingBudget),
                new FrameTimeCapBudget(staticSettings.FrameTimeCap, profilingProvider, container.LoadingStatus.IsLoadingScreenOn),
                new MemoryBudget(memoryCap, profilingProvider, staticSettings.MemoryThresholds),
                new SceneMapping()
            );

            DebugWidgetBuilder? cacheWidget = container.DebugContainerBuilder.TryAddWidget("Cache Textures");
            container.CacheCleaner = new CacheCleaner(sharedDependencies.FrameTimeBudget, cacheWidget);

            container.CharacterContainer = new CharacterContainer(container.assetsProvisioner, exposedGlobalDataContainer.ExposedCameraData, exposedPlayerTransform);
            container.MediaContainer = new MediaPlayerContainer(assetsProvisioner, webRequestsContainer.WebRequestController, volumeBus, sharedDependencies.FrameTimeBudget, container.RoomHubProxy, container.CacheCleaner);

            bool result = await InitializeContainersAsync(container, settingsContainer, ct);

            if (!result)
                return (null, false);


            container.QualityContainer = await QualityContainer.CreateAsync(settingsContainer, container.assetsProvisioner);
            container.ComponentsContainer = componentsContainer;
            container.SingletonSharedDependencies = sharedDependencies;
            container.Profiler = profilingProvider;
            container.EntityCollidersGlobalCache = new EntityCollidersGlobalCache();
            container.ExposedGlobalDataContainer = exposedGlobalDataContainer;
            container.WebRequestsContainer = webRequestsContainer;
            container.PortableExperiencesController = new ECSPortableExperiencesController(web3IdentityProvider, container.WebRequestsContainer.WebRequestController, container.ScenesCache, launchMode, decentralandUrlsSource);
            container.FeatureFlagsProvider = new HttpFeatureFlagsProvider(container.WebRequestsContainer.WebRequestController);

            ArrayPool<byte> buffersPool = ArrayPool<byte>.Create(1024 * 1024 * 50, 50);

            IGltfContainerAssetsCache gltfContainerAssetsCache = new GltfContainerAssetsCache(componentsContainer.ComponentPoolsRegistry);
            var gltfPlugin = new GltfContainerPlugin(sharedDependencies, container.CacheCleaner, container.SceneReadinessReportQueue, componentsContainer.ComponentPoolsRegistry, launchMode, useRemoteAssetBundles, container.WebRequestsContainer.WebRequestController, container.LoadingStatus, gltfContainerAssetsCache);
            var assetBundlePlugin = new AssetBundlesPlugin(reportHandlingSettings, container.CacheCleaner, container.WebRequestsContainer.WebRequestController, buffersPool, partialsDiskCache, URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.AssetBundlesCDN)), URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.AssetBundleRegistryVersion)), gltfContainerAssetsCache);

            var textureDiskCache = new DiskCache<TextureData, SerializeMemoryIterator<TextureDiskSerializer.State>>(diskCache, new TextureDiskSerializer());
            var textureResolvePlugin = new TexturesLoadingPlugin(container.WebRequestsContainer.WebRequestController, container.CacheCleaner, textureDiskCache, launchMode, profileRepository);

            diagnosticsContainer.AddSentryScopeConfigurator(scope =>
            {
                if (container.ScenesCache.CurrentScene != null)
                    diagnosticsContainer.Sentry!.AddCurrentSceneToScope(scope, container.ScenesCache.CurrentScene.Info);
            });

            diagnosticsContainer.AddSentryScopeConfigurator(scope =>
            {
                diagnosticsContainer.Sentry?.AddRealmInfoToScope(scope,
                    container.RealmData.Ipfs.CatalystBaseUrl.Value,
                    container.RealmData.Ipfs.ContentBaseUrl.Value,
                    container.RealmData.Ipfs.LambdasBaseUrl.Value);
            });

            var renderFeature = container.QualityContainer.RendererFeaturesCache.GetRendererFeature<GPUInstancingRenderFeature>();
            if (enableGPUInstancing && renderFeature != null && renderFeature.Settings != null && renderFeature.Settings.FrustumCullingAndLODGenComputeShader != null)
            {
                container.GPUInstancingService = new GPUInstancingService(renderFeature.Settings);
                renderFeature.Initialize(container.GPUInstancingService, container.RealmData);
            }
            else
                ReportHub.LogError("No renderer feature presented.", ReportCategory.GPU_INSTANCING);

            var promisesAnalyticsPlugin = new PromisesAnalyticsPlugin(debugContainerBuilder);

            container.ECSWorldPlugins = new IDCLWorldPlugin[]
            {
                gltfPlugin,
                new TransformsPlugin(sharedDependencies, exposedPlayerTransform, exposedGlobalDataContainer.ExposedCameraData),
                new BillboardPlugin(exposedGlobalDataContainer.ExposedCameraData),
                new NFTShapePlugin(decentralandUrlsSource, container.assetsProvisioner, sharedDependencies.FrameTimeBudget, componentsContainer.ComponentPoolsRegistry, container.WebRequestsContainer.WebRequestController, container.CacheCleaner, container.MediaContainer.mediaFactoryBuilder),
                new TextShapePlugin(sharedDependencies.FrameTimeBudget, container.CacheCleaner, componentsContainer.ComponentPoolsRegistry, assetsProvisioner),
                new MaterialsPlugin(sharedDependencies, container.MediaContainer.mediaFactoryBuilder),
                textureResolvePlugin,
                new AssetsCollidersPlugin(sharedDependencies),
                new AvatarShapePlugin(globalWorld, componentsContainer.ComponentPoolsRegistry, launchMode),
                new AvatarAttachPlugin(globalWorld, container.MainPlayerAvatarBaseProxy, componentsContainer.ComponentPoolsRegistry, container.EntityParticipantTableProxy),
                new PrimitivesRenderingPlugin(sharedDependencies),
                new VisibilityPlugin(),
                new AudioSourcesPlugin(sharedDependencies, container.WebRequestsContainer.WebRequestController, container.CacheCleaner, container.assetsProvisioner),
                assetBundlePlugin,
                new InteractionPlugin(sharedDependencies, profilingProvider, exposedGlobalDataContainer.GlobalInputEvents, componentsContainer.ComponentPoolsRegistry, container.assetsProvisioner),
                new SceneUIPlugin(sharedDependencies, container.assetsProvisioner, container.InputBlock),
                container.CharacterContainer.CreateWorldPlugin(componentsContainer.ComponentPoolsRegistry),
                new AnimatorPlugin(),
                new TweenPlugin(),
                container.MediaContainer.CreatePlugin(exposedGlobalDataContainer.ExposedCameraData),
                new SDKEntityTriggerAreaPlugin(globalWorld, container.MainPlayerAvatarBaseProxy, exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy, container.CharacterContainer.CharacterObject, componentsContainer.ComponentPoolsRegistry, container.assetsProvisioner, container.CacheCleaner, exposedGlobalDataContainer.ExposedCameraData, container.SceneRestrictionBusController, web3IdentityProvider, componentsContainer.ComponentPoolsRegistry.AddComponentPool<PBTriggerAreaResult.Types.Trigger>()),
                new PointerInputAudioPlugin(container.assetsProvisioner),
                new MapPinPlugin(globalWorld, container.MapPinsEventBus),
                new MultiplayerPlugin(),
                new RealmInfoPlugin(container.RealmData, container.RoomHubProxy),
                new InputModifierPlugin(globalWorld, container.PlayerEntity, container.SceneRestrictionBusController),
                new MainCameraPlugin(componentsContainer.ComponentPoolsRegistry, container.assetsProvisioner, container.CacheCleaner, exposedGlobalDataContainer.ExposedCameraData, container.SceneRestrictionBusController, globalWorld),
                new LightSourcePlugin(componentsContainer.ComponentPoolsRegistry, container.assetsProvisioner, container.CacheCleaner, container.CharacterContainer.CharacterObject, globalWorld, hasDebugFlag),
                new PrimaryPointerInfoPlugin(globalWorld),
                promisesAnalyticsPlugin,
                new SkyboxTimePlugin(),
#if UNITY_EDITOR
                new GizmosWorldPlugin(),
#endif
            };

            container.SceneLoadingLimit = new SceneLoadingLimit(container.MemoryCap);

            container.SharedPlugins = new IDCLGlobalPlugin[]
            {
                assetBundlePlugin,
                textureResolvePlugin,
                promisesAnalyticsPlugin,
                gltfPlugin
            };

            container.LandscapeParcelController = new LandscapeParcelController(
                    assetsProvisioner,
                    new LandscapeParcelService(webRequestsContainer.WebRequestController,
                        environment.Equals(DecentralandEnvironment.Zone)),
                    container.LandscapeParcelData
                );

            return (container, true);
        }

        private static async UniTask<bool> InitializeContainersAsync(StaticContainer target, IPluginSettingsContainer settings, CancellationToken ct)
        {
            ((StaticContainer plugin, bool success), (CharacterContainer plugin, bool success), (MediaPlayerContainer plugin, bool success)) results = await UniTask.WhenAll(
                settings.InitializePluginAsync(target, ct),
                settings.InitializePluginAsync(target.CharacterContainer, ct),
                settings.InitializePluginAsync(target.MediaContainer, ct)
            );

            return results.Item1.success && results.Item2.success && results.Item3.success;
        }
    }
}
