using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using Diagnostics;
using Diagnostics.ReportsHandling;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.Profiling;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
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

        public CameraSamplingData CameraSamplingData { get; private set; }

        public IReadOnlyList<IDCLWorldPlugin> ECSWorldPlugins { get; private set; }

        /// <summary>
        ///     Some plugins may implement both interfaces
        /// </summary>
        public IReadOnlyList<IDCLGlobalPlugin> SharedPlugins { get; private set; }

        public ECSWorldSingletonSharedDependencies SingletonSharedDependencies { get; private set; }

        public IProfilingProvider ProfilingProvider { get; private set; }

        public async UniTask Initialize(StaticSettings settings, CancellationToken ct)
        {
            (characterObject, reportHandlingSettings, partitionSettings, realmPartitionSettings) =
                await UniTask.WhenAll(
                    AssetsProvisioner.ProvideInstance(settings.CharacterObject, new Vector3(0f, settings.StartYPosition, 0f), Quaternion.identity, ct: ct),
                    AssetsProvisioner.ProvideMainAsset(settings.ReportHandlingSettings, ct),
                    AssetsProvisioner.ProvideMainAsset(settings.PartitionSettings, ct),
                    AssetsProvisioner.ProvideMainAsset(settings.RealmPartitionSettings, ct));
        }

        public void Dispose()
        {
            DiagnosticsContainer?.Dispose();
            characterObject.Dispose();
            realmPartitionSettings.Dispose();
            partitionSettings.Dispose();
            reportHandlingSettings.Dispose();
        }

        public IAssetsProvisioner AssetsProvisioner { get; private set; }

        /// <summary>
        ///     Character Object exists in a single instance
        /// </summary>
        public ICharacterObject CharacterObject => characterObject.Value;

        public IReportsHandlingSettings ReportHandlingSettings => reportHandlingSettings.Value;

        public IPartitionSettings PartitionSettings => partitionSettings.Value;

        public IRealmPartitionSettings RealmPartitionSettings => realmPartitionSettings.Value;

        public static async UniTask<(StaticContainer container, bool success)> Create(IPluginSettingsContainer settingsContainer, CancellationToken ct)
        {
            var componentsContainer = ComponentsContainer.Create();
            var profilingProvider = new ProfilingProvider();

            var container = new StaticContainer();
            var addressablesProvisioner = new AddressablesProvisioner();
            container.AssetsProvisioner = addressablesProvisioner;

            (_, bool result) = await settingsContainer.InitializePlugin(container, ct);

            if (!result)
                return (null, false);

            var sharedDependencies = new ECSWorldSingletonSharedDependencies(
                componentsContainer.ComponentPoolsRegistry,
                container.ReportHandlingSettings,
                new EntityFactory(),
                new PartitionedWorldsAggregate.Factory(),
                new ConcurrentLoadingBudgetProvider(50),
                new FrameTimeCapBudgetProvider(40, profilingProvider)
            );

            container.DiagnosticsContainer = DiagnosticsContainer.Create(container.ReportHandlingSettings);
            container.ComponentsContainer = componentsContainer;
            container.SingletonSharedDependencies = sharedDependencies;
            container.CameraSamplingData = new CameraSamplingData();
            container.ProfilingProvider = profilingProvider;

            var assetBundlePlugin = new AssetBundlesPlugin(container.ReportHandlingSettings);

            container.ECSWorldPlugins = new IDCLWorldPlugin[]
            {
                new TransformsPlugin(sharedDependencies),
                new MaterialsPlugin(sharedDependencies, addressablesProvisioner),
                new PrimitiveCollidersPlugin(sharedDependencies),
                new TexturesLoadingPlugin(),
                new PrimitivesRenderingPlugin(sharedDependencies),
                new VisibilityPlugin(),
                assetBundlePlugin,
                new GltfContainerPlugin(sharedDependencies),
            };

            container.SharedPlugins = new IDCLGlobalPlugin[]
            {
                assetBundlePlugin,
            };

            return (container, true);
        }
    }
}
