using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.DebugUtilities;
using DCL.DebugUtilities.Builders;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using ECS;
using ECS.Prioritization.Components;
using SceneRunner.EmptyScene;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public class DynamicWorldContainer : IDCLPlugin<DynamicWorldSettings>
    {
        private static readonly URLDomain ASSET_BUNDLES_URL = URLDomain.FromString("https://ab-cdn.decentraland.org/");

        public DebugUtilitiesContainer DebugContainer { get; private set; }

        public IRealmController RealmController { get; private set; }

        public GlobalWorldFactory GlobalWorldFactory { get; private set; }

        public EmptyScenesWorldFactory EmptyScenesWorldFactory { get; private set; }

        public IReadOnlyList<IDCLGlobalPlugin> GlobalPlugins { get; private set; }

        public void Dispose() { }

        public static async UniTask<(DynamicWorldContainer container, bool success)> CreateAsync(
            StaticContainer staticContainer,
            IPluginSettingsContainer settingsContainer,
            CancellationToken ct,
            UIDocument rootUIDocument,
            IReadOnlyList<int2> staticLoadPositions, int sceneLoadRadius)
        {
            var container = new DynamicWorldContainer();
            (_, bool result) = await settingsContainer.InitializePluginAsync(container, ct);

            if (!result)
                return (null, false);

            DebugContainerBuilder debugBuilder = container.DebugContainer.Builder;

            var realmSamplingData = new RealmSamplingData();
            var dclInput = new DCLInput();
            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;
            var realmData = new RealmData();

            var globalPlugins = new List<IDCLGlobalPlugin>
            {
                new CharacterMotionPlugin(staticContainer.AssetsProvisioner, staticContainer.CharacterObject),
                new InputPlugin(dclInput),
                new GlobalInteractionPlugin(dclInput, rootUIDocument, staticContainer.AssetsProvisioner, staticContainer.EntityCollidersGlobalCache, exposedGlobalDataContainer.GlobalInputEvents),
                new CharacterCameraPlugin(staticContainer.AssetsProvisioner, realmSamplingData, exposedGlobalDataContainer.CameraSamplingData, exposedGlobalDataContainer.ExposedCameraData),
                new ProfilingPlugin(staticContainer.ProfilingProvider, debugBuilder),
                new WearablePlugin(staticContainer.AssetsProvisioner, realmData, ASSET_BUNDLES_URL),
                new AvatarPlugin(staticContainer.ComponentsContainer.ComponentPoolsRegistry, staticContainer.AssetsProvisioner,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudgetProvider, realmData, debugBuilder),
            };

            globalPlugins.AddRange(staticContainer.SharedPlugins);

            container.RealmController = new RealmController(sceneLoadRadius, staticLoadPositions, realmData);

            container.GlobalWorldFactory = new GlobalWorldFactory(in staticContainer, staticContainer.RealmPartitionSettings,
                exposedGlobalDataContainer.CameraSamplingData, realmSamplingData, ASSET_BUNDLES_URL, realmData, globalPlugins);

            container.GlobalPlugins = globalPlugins.Concat(staticContainer.SharedPlugins).ToList();
            container.EmptyScenesWorldFactory = new EmptyScenesWorldFactory(staticContainer.SingletonSharedDependencies, staticContainer.ECSWorldPlugins);

            return (container, true);
        }

        public UniTask InitializeAsync(DynamicWorldSettings settings, CancellationToken ct)
        {
            DebugContainer = DebugUtilitiesContainer.Create(settings.DebugViewsCatalog);
            return UniTask.CompletedTask;
        }
    }
}
