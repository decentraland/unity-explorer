using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.Diagnostics;
using DCL.Gizmos.Plugin;
using DCL.Interaction.Utility;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.WebRequests.Analytics;
using DCL.Profiling;
using DCL.ResourcesUnloading;
using DCL.Time;
using ECS.Prioritization;
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

        public static async UniTask<(StaticContainer container, bool success)> CreateAsync(IPluginSettingsContainer settingsContainer, CancellationToken ct)
        {
            ProfilingCounters.CleanAllCounters();

            var componentsContainer = ComponentsContainer.Create();
            var exposedGlobalDataContainer = ExposedGlobalDataContainer.Create();
            var profilingProvider = new ProfilingProvider();

            var container = new StaticContainer();
            var addressablesProvisioner = new AddressablesProvisioner();
            container.AssetsProvisioner = addressablesProvisioner;

            (_, bool result) = await settingsContainer.InitializePluginAsync(container, ct);

            if (!result)
                return (null, false);

            StaticSettings staticSettings = settingsContainer.GetSettings<StaticSettings>();

            var sharedDependencies = new ECSWorldSingletonSharedDependencies(
                componentsContainer.ComponentPoolsRegistry,
                container.ReportHandlingSettings,
                new SceneEntityFactory(),
                new PartitionedWorldsAggregate.Factory(),
                new ConcurrentLoadingBudgetProvider(staticSettings.AssetsLoadingBudget),
                new FrameTimeCapBudgetProvider(staticSettings.FrameTimeCap, profilingProvider),
                new MemoryBudgetProvider(profilingProvider, staticSettings.MemoryThresholds)
            );

            container.CacheCleaner = new CacheCleaner(sharedDependencies.FrameTimeBudgetProvider);

            container.DiagnosticsContainer = DiagnosticsContainer.Create(container.ReportHandlingSettings);
            container.ComponentsContainer = componentsContainer;
            container.SingletonSharedDependencies = sharedDependencies;
            container.ProfilingProvider = profilingProvider;
            container.EntityCollidersGlobalCache = new EntityCollidersGlobalCache();
            container.ExposedGlobalDataContainer = exposedGlobalDataContainer;
            container.WebRequestsContainer = WebRequestsContainer.Create();
            container.PhysicsTickProvider = new PhysicsTickProvider();

            var assetBundlePlugin = new AssetBundlesPlugin(container.ReportHandlingSettings, container.CacheCleaner);

            container.ECSWorldPlugins = new IDCLWorldPlugin[]
            {
                new TransformsPlugin(sharedDependencies),
                new MaterialsPlugin(sharedDependencies, addressablesProvisioner),
                new TexturesLoadingPlugin(container.WebRequestsContainer.WebRequestController, container.CacheCleaner),
                new AssetsCollidersPlugin(sharedDependencies, container.PhysicsTickProvider),

                new PrimitivesRenderingPlugin(sharedDependencies),
                new VisibilityPlugin(),
                new AudioPlugin(sharedDependencies, container.WebRequestsContainer.WebRequestController),
                assetBundlePlugin,
                new GltfContainerPlugin(sharedDependencies, container.CacheCleaner),
                new InteractionPlugin(sharedDependencies, profilingProvider, exposedGlobalDataContainer.GlobalInputEvents),
#if UNITY_EDITOR
                new GizmosWorldPlugin(),
#endif
            };

            container.SharedPlugins = new IDCLGlobalPlugin[]
            {
                assetBundlePlugin,
                new ResourceUnloadingPlugin(sharedDependencies.MemoryBudgetProvider, container.CacheCleaner),
            };

            return (container, true);
        }
    }
}
