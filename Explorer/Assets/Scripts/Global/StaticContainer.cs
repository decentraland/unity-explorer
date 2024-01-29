using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.Diagnostics;
using DCL.Gizmos.Plugin;
using DCL.Interaction.Utility;
using DCL.MapRenderer.ComponentsFactory;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiling;
using DCL.ResourcesUnloading;
using DCL.Time;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using ECS.Prioritization;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Global
{
    /// <summary>
    ///     Produces dependencies that never change during the lifetime of the application
    ///     and are not connected to the global world or scenes but are used by them.
    ///     This is the first container to instantiate, should not depend on any other container
    /// </summary>
    public class StaticContainer : IDCLPlugin<StaticSettings>
    {
        public WorldProxy GlobalWorld = new ();
        private ProvidedInstance<CharacterObject> characterObject;
        private ProvidedAsset<PartitionSettingsAsset> partitionSettings;
        private ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings;
        private ProvidedAsset<ReportsHandlingSettings> reportHandlingSettings;

        public DiagnosticsContainer DiagnosticsContainer { get; private set; }

        public ComponentsContainer ComponentsContainer { get; private set; }

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

        public IAssetsProvisioner AssetsProvisioner { get; private set; }

        /// <summary>
        ///     Character Object exists in a single instance
        /// </summary>
        public ICharacterObject CharacterObject => characterObject.Value;

        public IReportsHandlingSettings ReportHandlingSettings => reportHandlingSettings.Value;

        public IPartitionSettings PartitionSettings => partitionSettings.Value;

        public IRealmPartitionSettings RealmPartitionSettings => realmPartitionSettings.Value;
        public StaticSettings StaticSettings { get; private set; }
        public CacheCleaner CacheCleaner { get; private set; }
        public IEthereumApi EthereumApi { get; private set; }
        public IScenesCache ScenesCache { get; private set; }
        public ISceneReadinessReportQueue SceneReadinessReportQueue { get; private set; }

        public void Dispose()
        {
            DiagnosticsContainer?.Dispose();
            characterObject.Dispose();
            realmPartitionSettings.Dispose();
            partitionSettings.Dispose();
            reportHandlingSettings.Dispose();
        }

        public async UniTask InitializeAsync(StaticSettings settings, CancellationToken ct)
        {
            StaticSettings = settings;

            (characterObject, reportHandlingSettings, partitionSettings, realmPartitionSettings) =
                await UniTask.WhenAll(
                    AssetsProvisioner.ProvideInstanceAsync(settings.CharacterObject, new Vector3(0f, settings.StartYPosition, 0f), Quaternion.identity, ct: ct),
                    AssetsProvisioner.ProvideMainAssetAsync(settings.ReportHandlingSettings, ct),
                    AssetsProvisioner.ProvideMainAssetAsync(settings.PartitionSettings, ct),
                    AssetsProvisioner.ProvideMainAssetAsync(settings.RealmPartitionSettings, ct));
        }

        public static async UniTask<(StaticContainer container, bool success)> CreateAsync(
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

            container.EthereumApi = ethereumApi;
            container.ScenesCache = new ScenesCache();
            container.SceneReadinessReportQueue = new SceneReadinessReportQueue(container.ScenesCache);

            var addressablesProvisioner = new AddressablesProvisioner();
            container.AssetsProvisioner = addressablesProvisioner;

            (_, bool result) = await settingsContainer.InitializePluginAsync(container, ct);

            if (!result)
                return (null, false)!;

            StaticSettings staticSettings = settingsContainer.GetSettings<StaticSettings>();

            var sharedDependencies = new ECSWorldSingletonSharedDependencies(
                componentsContainer.ComponentPoolsRegistry,
                container.ReportHandlingSettings,
                new SceneEntityFactory(),
                new PartitionedWorldsAggregate.Factory(),
                new ConcurrentLoadingPerformanceBudget(staticSettings.AssetsLoadingBudget),
                new FrameTimeCapBudget(staticSettings.FrameTimeCap, profilingProvider),
                new MemoryBudget(new StandaloneSystemMemory(), profilingProvider, staticSettings.MemoryThresholds)
            );

            container.CacheCleaner = new CacheCleaner(sharedDependencies.FrameTimeBudget);

            container.DiagnosticsContainer = DiagnosticsContainer.Create(container.ReportHandlingSettings);
            container.ComponentsContainer = componentsContainer;
            container.SingletonSharedDependencies = sharedDependencies;
            container.ProfilingProvider = profilingProvider;
            container.EntityCollidersGlobalCache = new EntityCollidersGlobalCache();
            container.ExposedGlobalDataContainer = exposedGlobalDataContainer;
            container.WebRequestsContainer = WebRequestsContainer.Create(web3IdentityProvider);
            container.PhysicsTickProvider = new PhysicsTickProvider();

            var assetBundlePlugin = new AssetBundlesPlugin(container.ReportHandlingSettings, container.CacheCleaner);
            var textureResolvePlugin = new TexturesLoadingPlugin(container.WebRequestsContainer.WebRequestController, container.CacheCleaner);

            container.ECSWorldPlugins = new IDCLWorldPlugin[]
            {
                new TransformsPlugin(sharedDependencies),
                new BillboardPlugin(exposedGlobalDataContainer.ExposedCameraData),
                new NFTShapePlugin(container.AssetsProvisioner, sharedDependencies.FrameTimeBudget, componentsContainer.ComponentPoolsRegistry, container.WebRequestsContainer.WebRequestController, container.CacheCleaner),
                new TextShapePlugin(sharedDependencies.FrameTimeBudget, componentsContainer.ComponentPoolsRegistry, settingsContainer),
                new MaterialsPlugin(sharedDependencies, addressablesProvisioner),
                textureResolvePlugin,
                new AssetsCollidersPlugin(sharedDependencies, container.PhysicsTickProvider),
                new AvatarShapePlugin(container.GlobalWorld),
                new PrimitivesRenderingPlugin(sharedDependencies),
                new VisibilityPlugin(),
                new AudioSourcesPlugin(sharedDependencies, container.WebRequestsContainer.WebRequestController, container.CacheCleaner),
                assetBundlePlugin,
                new GltfContainerPlugin(sharedDependencies, container.CacheCleaner),
                new InteractionPlugin(sharedDependencies, profilingProvider, exposedGlobalDataContainer.GlobalInputEvents),
                new SceneUIPlugin(sharedDependencies, addressablesProvisioner),
                new AudioStreamPlugin(sharedDependencies, container.CacheCleaner),

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
