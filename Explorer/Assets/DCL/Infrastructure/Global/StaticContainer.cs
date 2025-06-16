using Arch.Core;
using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.Character.Plugin;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Gizmos.Plugin;
using DCL.Input;
using DCL.Interaction.Utility;
using DCL.MapPins.Bus;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Optimization.AdaptivePerformance.Systems;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiling;
using DCL.Quality;
using DCL.ResourcesUnloading;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SDKComponents.VideoPlayer;
using DCL.Settings;
using DCL.Time;
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
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using Global.Dynamic.LaunchModes;
using PortableExperiences.Controller;
using System.Buffers;
using UnityEngine;
using UnityEngine.UIElements;
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
        public readonly ObjectProxy<DCLInput> InputProxy = new ();
        public readonly ObjectProxy<AvatarBase> MainPlayerAvatarBaseProxy = new ();
        public readonly ObjectProxy<IRoomHub> RoomHubProxy = new ();
        public readonly ObjectProxy<IReadOnlyEntityParticipantTable> EntityParticipantTableProxy = new ();
        public readonly RealmData RealmData = new ();
        public readonly PartitionDataContainer PartitionDataContainer = new ();
        public readonly IMapPinsEventBus MapPinsEventBus = new MapPinsEventBus();

        private ProvidedInstance<CharacterObject> characterObject;
        private ProvidedAsset<PartitionSettingsAsset> partitionSettings;
        private ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings;

        private IAssetsProvisioner assetsProvisioner;
        public Entity PlayerEntity { get; set; }

        public ComponentsContainer ComponentsContainer { get; private set; }
        public CharacterContainer CharacterContainer { get; private set; }
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
        public PhysicsTickProvider PhysicsTickProvider { get; private set; }
        public IEntityCollidersGlobalCache EntityCollidersGlobalCache { get; private set; }
        public IPartitionSettings PartitionSettings => partitionSettings.Value;
        public IRealmPartitionSettings RealmPartitionSettings => realmPartitionSettings.Value;
        public StaticSettings StaticSettings { get; private set; }
        public CacheCleaner CacheCleaner { get; private set; }
        public IEthereumApi EthereumApi { get; private set; }
        public IInputBlock InputBlock { get; private set; }
        public IScenesCache ScenesCache { get; private set; }
        public ISceneReadinessReportQueue SceneReadinessReportQueue { get; private set; }
        public IFeatureFlagsProvider FeatureFlagsProvider { get; private set; }
        public IPortableExperiencesController PortableExperiencesController { get; private set; }
        public IDebugContainerBuilder DebugContainerBuilder { get; private set; }
        public ISceneRestrictionBusController SceneRestrictionBusController { get; private set; }
        public GPUInstancingService GPUInstancingService { get; private set; }

        public ILoadingStatus LoadingStatus { get; private set; }
        public ILaunchMode LaunchMode { get; private set; }

        public void Dispose()
        {
            realmPartitionSettings.Dispose();
            partitionSettings.Dispose();
            QualityContainer.Dispose();
            Profiler.Dispose();
            SceneRestrictionBusController.Dispose();
        }

        public async UniTask InitializeAsync(StaticSettings settings, CancellationToken ct)
        {
            StaticSettings = settings;

            (partitionSettings, realmPartitionSettings) =
                await UniTask.WhenAll(
                    assetsProvisioner.ProvideMainAssetAsync(settings.PartitionSettings, ct, nameof(PartitionSettings)),
                    assetsProvisioner.ProvideMainAssetAsync(settings.RealmPartitionSettings, ct, nameof(RealmPartitionSettings))
                );
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
            WorldVolumeMacBus worldVolumeMacBus,
            bool enableAnalytics,
            IAnalyticsController analyticsController,
            IDiskCache diskCache,
            IDiskCache<PartialLoadingState> partialsDiskCache,
            UIDocument scenesUIRoot,
            ObjectProxy<IProfileRepository> profileRepository,
            CancellationToken ct,
            bool enableGPUInstancing = true)
        {
            ProfilingCounters.CleanAllCounters();

            var componentsContainer = ComponentsContainer.Create();
            var exposedGlobalDataContainer = ExposedGlobalDataContainer.Create();
            var profilingProvider = new Profiler();
            var container = new StaticContainer();
            var dclInput = new DCLInput();

            container.InputProxy.SetObject(dclInput);
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

            container.CharacterContainer = new CharacterContainer(container.assetsProvisioner, exposedGlobalDataContainer.ExposedCameraData, exposedPlayerTransform);

            bool result = await InitializeContainersAsync(container, settingsContainer, ct);

            if (!result)
                return (null, false);

            StaticSettings staticSettings = settingsContainer.GetSettings<StaticSettings>();

            var sharedDependencies = new ECSWorldSingletonSharedDependencies(
                componentsContainer.ComponentPoolsRegistry,
                reportHandlingSettings,
                new SceneEntityFactory(),
                new PartitionedWorldsAggregate.Factory(),
                new ConcurrentLoadingPerformanceBudget(staticSettings.AssetsLoadingBudget),
                new FrameTimeCapBudget(staticSettings.FrameTimeCap, profilingProvider),
                new MemoryBudget(memoryCap, profilingProvider, staticSettings.MemoryThresholds),
                new SceneMapping()
            );

            DebugWidgetBuilder? cacheWidget = container.DebugContainerBuilder.TryAddWidget("Cache Textures");

            container.QualityContainer = await QualityContainer.CreateAsync(settingsContainer, container.assetsProvisioner);
            container.CacheCleaner = new CacheCleaner(sharedDependencies.FrameTimeBudget, cacheWidget);
            container.ComponentsContainer = componentsContainer;
            container.SingletonSharedDependencies = sharedDependencies;
            container.Profiler = profilingProvider;
            container.EntityCollidersGlobalCache = new EntityCollidersGlobalCache();
            container.ExposedGlobalDataContainer = exposedGlobalDataContainer;
            container.WebRequestsContainer = webRequestsContainer;
            container.PhysicsTickProvider = new PhysicsTickProvider();

            container.PortableExperiencesController = new ECSPortableExperiencesController(web3IdentityProvider, container.WebRequestsContainer.WebRequestController, container.ScenesCache, launchMode, decentralandUrlsSource);

            container.FeatureFlagsProvider = new HttpFeatureFlagsProvider(container.WebRequestsContainer.WebRequestController);

            ArrayPool<byte> buffersPool = ArrayPool<byte>.Create(1024 * 1024 * 50, 50);
            var textureDiskCache = new DiskCache<Texture2DData, SerializeMemoryIterator<TextureDiskSerializer.State>>(diskCache, new TextureDiskSerializer());
            var assetBundlePlugin = new AssetBundlesPlugin(reportHandlingSettings, container.CacheCleaner, container.WebRequestsContainer.WebRequestController, buffersPool, partialsDiskCache);
            var textureResolvePlugin = new TexturesLoadingPlugin(container.WebRequestsContainer.WebRequestController, container.CacheCleaner, textureDiskCache, launchMode, profileRepository);

            ExtendedObjectPool<Texture2D> videoTexturePool = VideoTextureFactory.CreateVideoTexturesPool();

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


            container.LoadingStatus = enableAnalytics ? new LoadingStatusAnalyticsDecorator(new LoadingStatus(), analyticsController) : new LoadingStatus();

            var promisesAnalyticsPlugin = new PromisesAnalyticsPlugin(debugContainerBuilder);

            container.ECSWorldPlugins = new IDCLWorldPlugin[]
            {
                new TransformsPlugin(sharedDependencies, exposedPlayerTransform, exposedGlobalDataContainer.ExposedCameraData),
                new BillboardPlugin(exposedGlobalDataContainer.ExposedCameraData),
                new NFTShapePlugin(decentralandUrlsSource, container.assetsProvisioner, sharedDependencies.FrameTimeBudget, componentsContainer.ComponentPoolsRegistry, container.WebRequestsContainer.WebRequestController, container.CacheCleaner, textureDiskCache, videoTexturePool),
                new TextShapePlugin(sharedDependencies.FrameTimeBudget, container.CacheCleaner, componentsContainer.ComponentPoolsRegistry),
                new MaterialsPlugin(sharedDependencies, videoTexturePool),
                textureResolvePlugin,
                new AssetsCollidersPlugin(sharedDependencies, container.PhysicsTickProvider),
                new AvatarShapePlugin(globalWorld, componentsContainer.ComponentPoolsRegistry),
                new AvatarAttachPlugin(globalWorld, container.MainPlayerAvatarBaseProxy, componentsContainer.ComponentPoolsRegistry, container.EntityParticipantTableProxy),
                new PrimitivesRenderingPlugin(sharedDependencies),
                new VisibilityPlugin(),
                new AudioSourcesPlugin(sharedDependencies, container.WebRequestsContainer.WebRequestController, container.CacheCleaner, container.assetsProvisioner),
                assetBundlePlugin,
                new GltfContainerPlugin(sharedDependencies, container.CacheCleaner, container.SceneReadinessReportQueue, componentsContainer.ComponentPoolsRegistry, launchMode, useRemoteAssetBundles, container.WebRequestsContainer.WebRequestController, container.LoadingStatus),
                new InteractionPlugin(sharedDependencies, profilingProvider, exposedGlobalDataContainer.GlobalInputEvents, componentsContainer.ComponentPoolsRegistry, container.assetsProvisioner),
                new SceneUIPlugin(sharedDependencies, container.assetsProvisioner, container.InputBlock, container.InputProxy, scenesUIRoot),
                container.CharacterContainer.CreateWorldPlugin(componentsContainer.ComponentPoolsRegistry),
                new AnimatorPlugin(),
                new TweenPlugin(),
                new MediaPlayerPlugin(videoTexturePool, sharedDependencies.FrameTimeBudget, container.assetsProvisioner, container.WebRequestsContainer.WebRequestController, container.CacheCleaner, worldVolumeMacBus, exposedGlobalDataContainer.ExposedCameraData, container.RoomHubProxy),
                new CharacterTriggerAreaPlugin(globalWorld, container.MainPlayerAvatarBaseProxy, exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy, container.CharacterContainer.CharacterObject, componentsContainer.ComponentPoolsRegistry, container.assetsProvisioner, container.CacheCleaner, exposedGlobalDataContainer.ExposedCameraData, container.SceneRestrictionBusController, web3IdentityProvider),
                new PointerInputAudioPlugin(container.assetsProvisioner),
                new MapPinPlugin(globalWorld, container.MapPinsEventBus),
                new MultiplayerPlugin(),
                new RealmInfoPlugin(container.RealmData, container.RoomHubProxy),
                new InputModifierPlugin(globalWorld, container.PlayerEntity, container.SceneRestrictionBusController),
                new MainCameraPlugin(componentsContainer.ComponentPoolsRegistry, container.assetsProvisioner, container.CacheCleaner, exposedGlobalDataContainer.ExposedCameraData, container.SceneRestrictionBusController, globalWorld),
                new LightSourcePlugin(componentsContainer.ComponentPoolsRegistry, container.assetsProvisioner, container.CacheCleaner),
                new PrimaryPointerInfoPlugin(globalWorld, container.InputProxy),
                promisesAnalyticsPlugin,
#if UNITY_EDITOR
                new GizmosWorldPlugin(),
#endif
            };

            container.SceneLoadingLimit = new SceneLoadingLimit(container.MemoryCap);

            container.SharedPlugins = new IDCLGlobalPlugin[]
            {
                assetBundlePlugin,
                new ResourceUnloadingPlugin(sharedDependencies.MemoryBudget, container.CacheCleaner, container.SceneLoadingLimit),
                new AdaptivePerformancePlugin(container.assetsProvisioner, container.Profiler, container.LoadingStatus),
                textureResolvePlugin,
                promisesAnalyticsPlugin,
            };

            return (container, true);
        }

        private static async UniTask<bool> InitializeContainersAsync(StaticContainer target, IPluginSettingsContainer settings, CancellationToken ct)
        {
            ((StaticContainer plugin, bool success), (CharacterContainer plugin, bool success)) results = await UniTask.WhenAll(
                settings.InitializePluginAsync(target, ct),
                settings.InitializePluginAsync(target.CharacterContainer, ct)
            );

            return results.Item1.success && results.Item2.success;
        }
    }
}
