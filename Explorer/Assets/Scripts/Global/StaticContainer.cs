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
using DCL.Input.UnityInputSystem.Blocks;
using DCL.Interaction.Utility;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiling;
using DCL.Quality;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.VideoPlayer;
using DCL.Time;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using ECS;
using ECS.Prioritization;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using System.Collections.Generic;
using System.Threading;
using ECS.SceneLifeCycle.Components;
using PortableExperiences.Controller;
using SceneRunner.Mapping;
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
        public readonly ObjectProxy<World> GlobalWorldProxy = new ();
        public readonly ObjectProxy<Entity> PlayerEntityProxy = new ();
        public readonly ObjectProxy<DCLInput> InputProxy = new ();
        public readonly ObjectProxy<AvatarBase> MainPlayerAvatarBaseProxy = new ();
        public readonly ObjectProxy<IRoomHub> RoomHubProxy = new ();
        public readonly RealmData RealmData = new ();
        public readonly PartitionDataContainer PartitionDataContainer = new ();

        private ProvidedInstance<CharacterObject> characterObject;
        private ProvidedAsset<PartitionSettingsAsset> partitionSettings;
        private ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings;

        public ComponentsContainer ComponentsContainer { get; private set; }

        public CharacterContainer CharacterContainer { get; private set; }

        public QualityContainer QualityContainer { get; private set; }

        public ExposedGlobalDataContainer ExposedGlobalDataContainer { get; private set; }

        public WebRequestsContainer WebRequestsContainer { get; private set; }

        public IReadOnlyList<IDCLWorldPlugin> ECSWorldPlugins { get; private set; }

        /// <summary>
        ///     Some plugins may implement both interfaces
        /// </summary>
        public IReadOnlyList<IDCLGlobalPlugin> SharedPlugins { get; private set; }

        public ECSWorldSingletonSharedDependencies SingletonSharedDependencies { get; private set; }

        public IProfilingProvider ProfilingProvider { get; private set; }

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
        public FeatureFlagsCache FeatureFlagsCache { get; private set; }
        public IFeatureFlagsProvider FeatureFlagsProvider { get; private set; }
        public IPortableExperiencesController PortableExperiencesController { get; private set; }

        public IDebugContainerBuilder DebugContainerBuilder { get; private set; }

        private IAssetsProvisioner assetsProvisioner;

        public void Dispose()
        {
            realmPartitionSettings.Dispose();
            partitionSettings.Dispose();
            QualityContainer.Dispose();
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

        private static async UniTask<bool> InitializeContainersAsync(StaticContainer target, IPluginSettingsContainer settings, CancellationToken ct)
        {
            ((StaticContainer plugin, bool success), (CharacterContainer plugin, bool success)) results = await UniTask.WhenAll(
                settings.InitializePluginAsync(target, ct),
                settings.InitializePluginAsync(target.CharacterContainer, ct)
            );

            return results.Item1.success && results.Item2.success;
        }

        public static async UniTask<(StaticContainer? container, bool success)> CreateAsync(
            IAssetsProvisioner assetsProvisioner,
            IReportsHandlingSettings reportHandlingSettings,
            DebugViewsCatalog debugViewsCatalog,
            IPluginSettingsContainer settingsContainer,
            IWeb3IdentityCache web3IdentityProvider,
            IEthereumApi ethereumApi,
            CancellationToken ct)
        {
            ProfilingCounters.CleanAllCounters();

            var componentsContainer = ComponentsContainer.Create();
            var exposedGlobalDataContainer = ExposedGlobalDataContainer.Create();
            var profilingProvider = new ProfilingProvider();

            var container = new StaticContainer();

            container.DebugContainerBuilder = DebugUtilitiesContainer.Create(debugViewsCatalog).Builder;
            container.EthereumApi = ethereumApi;
            container.ScenesCache = new ScenesCache();
            container.SceneReadinessReportQueue = new SceneReadinessReportQueue(container.ScenesCache);

            container.InputBlock = new InputBlock(container.InputProxy, container.GlobalWorldProxy, container.PlayerEntityProxy);

            container.assetsProvisioner = assetsProvisioner;
            var exposedPlayerTransform = new ExposedTransform();
            container.CharacterContainer = new CharacterContainer(container.assetsProvisioner, exposedGlobalDataContainer.ExposedCameraData, exposedPlayerTransform);

            bool result = await InitializeContainersAsync(container, settingsContainer, ct);

            if (!result)
                return (null, false)!;

            StaticSettings staticSettings = settingsContainer.GetSettings<StaticSettings>();

            var sharedDependencies = new ECSWorldSingletonSharedDependencies(
                componentsContainer.ComponentPoolsRegistry,
                reportHandlingSettings,
                new SceneEntityFactory(),
                new PartitionedWorldsAggregate.Factory(),
                new ConcurrentLoadingPerformanceBudget(staticSettings.AssetsLoadingBudget),
                new FrameTimeCapBudget(staticSettings.FrameTimeCap, profilingProvider),
                new MemoryBudget(new StandaloneSystemMemory(), profilingProvider, staticSettings.MemoryThresholds),
                new SceneAssetLock(),
                new SceneMapping()
            );

            container.QualityContainer = await QualityContainer.CreateAsync(settingsContainer, container.assetsProvisioner);
            container.CacheCleaner = new CacheCleaner(sharedDependencies.FrameTimeBudget);

            container.ComponentsContainer = componentsContainer;
            container.SingletonSharedDependencies = sharedDependencies;
            container.ProfilingProvider = profilingProvider;
            container.EntityCollidersGlobalCache = new EntityCollidersGlobalCache();
            container.ExposedGlobalDataContainer = exposedGlobalDataContainer;
            container.WebRequestsContainer = WebRequestsContainer.Create(web3IdentityProvider, container.DebugContainerBuilder);
            container.PhysicsTickProvider = new PhysicsTickProvider();
            container.PortableExperiencesController = new PortableExperiencesController(container.GlobalWorldProxy, web3IdentityProvider, container.WebRequestsContainer.WebRequestController, container.ScenesCache);

            container.FeatureFlagsCache = new FeatureFlagsCache();

            container.FeatureFlagsProvider = new HttpFeatureFlagsProvider(container.WebRequestsContainer.WebRequestController,
                container.FeatureFlagsCache);

            var assetBundlePlugin = new AssetBundlesPlugin(reportHandlingSettings, container.CacheCleaner);
            var textureResolvePlugin = new TexturesLoadingPlugin(container.WebRequestsContainer.WebRequestController, container.CacheCleaner);

            ExtendedObjectPool<Texture2D> videoTexturePool = VideoTextureFactory.CreateVideoTexturesPool();

            container.ECSWorldPlugins = new IDCLWorldPlugin[]
            {
                new TransformsPlugin(sharedDependencies, exposedPlayerTransform, exposedGlobalDataContainer.ExposedCameraData),
                new BillboardPlugin(exposedGlobalDataContainer.ExposedCameraData),
                new NFTShapePlugin(container.assetsProvisioner, sharedDependencies.FrameTimeBudget, componentsContainer.ComponentPoolsRegistry, container.WebRequestsContainer.WebRequestController, container.CacheCleaner),
                new TextShapePlugin(sharedDependencies.FrameTimeBudget, container.CacheCleaner, componentsContainer.ComponentPoolsRegistry),
                new MaterialsPlugin(sharedDependencies, container.assetsProvisioner, videoTexturePool),
                textureResolvePlugin,
                new AssetsCollidersPlugin(sharedDependencies, container.PhysicsTickProvider),
                new AvatarShapePlugin(container.GlobalWorldProxy),
                new AvatarAttachPlugin(container.MainPlayerAvatarBaseProxy, componentsContainer.ComponentPoolsRegistry),
                new PrimitivesRenderingPlugin(sharedDependencies),
                new VisibilityPlugin(),
                new AudioSourcesPlugin(sharedDependencies, container.WebRequestsContainer.WebRequestController, container.CacheCleaner),
                assetBundlePlugin, new GltfContainerPlugin(sharedDependencies, container.CacheCleaner, container.SceneReadinessReportQueue, container.SingletonSharedDependencies.SceneAssetLock),
                new InteractionPlugin(sharedDependencies, profilingProvider, exposedGlobalDataContainer.GlobalInputEvents, componentsContainer.ComponentPoolsRegistry, container.assetsProvisioner),
                new SceneUIPlugin(sharedDependencies, container.assetsProvisioner, container.InputBlock),
                container.CharacterContainer.CreateWorldPlugin(componentsContainer.ComponentPoolsRegistry),
                new AnimatorPlugin(),
                new TweenPlugin(),
                new MediaPlayerPlugin(sharedDependencies, videoTexturePool, sharedDependencies.FrameTimeBudget, container.assetsProvisioner, container.WebRequestsContainer.WebRequestController, container.CacheCleaner),
                new CharacterTriggerAreaPlugin(container.GlobalWorldProxy, container.MainPlayerAvatarBaseProxy, exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy, container.CharacterContainer.CharacterObject, componentsContainer.ComponentPoolsRegistry, container.assetsProvisioner, container.CacheCleaner),
                new InteractionsAudioPlugin(container.assetsProvisioner),
                new MapPinPlugin(container.GlobalWorldProxy),
                new MultiplayerPlugin(),
                new RealmInfoPlugin(container.RealmData, container.RoomHubProxy),

#if UNITY_EDITOR
                new GizmosWorldPlugin(),
#endif
            };

            container.SharedPlugins = new IDCLGlobalPlugin[]
            {
                assetBundlePlugin,
                new ResourceUnloadingPlugin(sharedDependencies.MemoryBudget, container.CacheCleaner),
                textureResolvePlugin,
            };

            return (container, true);
        }
    }
}
