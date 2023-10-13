using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using DCL.PluginSystem.Global;
using ECS;
using ECS.Prioritization.Components;
using SceneRunner.EmptyScene;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public class DynamicWorldContainer
    {
        private static readonly URLDomain ASSET_BUNDLES_URL = URLDomain.FromString("https://ab-cdn.decentraland.org/");

        public IRealmController RealmController { get; private set; }

        public GlobalWorldFactory GlobalWorldFactory { get; private set; }

        public EmptyScenesWorldFactory EmptyScenesWorldFactory { get; private set; }

        public IReadOnlyList<IDCLGlobalPlugin> GlobalPlugins { get; private set; }

        public static DynamicWorldContainer Create(
            in StaticContainer staticContainer,
            UIDocument rootUIDocument,
            IReadOnlyList<int2> staticLoadPositions, int sceneLoadRadius)
        {
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
                new ProfilingPlugin(staticContainer.AssetsProvisioner, staticContainer.ProfilingProvider),
                new WearablePlugin(staticContainer.AssetsProvisioner, realmData, ASSET_BUNDLES_URL),
                new AvatarPlugin(staticContainer.ComponentsContainer.ComponentPoolsRegistry, staticContainer.AssetsProvisioner, staticContainer.SingletonSharedDependencies.FrameTimeBudgetProvider, realmData)
            };

            globalPlugins.AddRange(staticContainer.SharedPlugins);

            return new DynamicWorldContainer
            {
                RealmController = new RealmController(sceneLoadRadius, staticLoadPositions, realmData),
                GlobalWorldFactory = new GlobalWorldFactory(in staticContainer, staticContainer.RealmPartitionSettings,
                    exposedGlobalDataContainer.CameraSamplingData, realmSamplingData, ASSET_BUNDLES_URL, realmData, globalPlugins),
                GlobalPlugins = globalPlugins.Concat(staticContainer.SharedPlugins).ToList(),
                EmptyScenesWorldFactory = new EmptyScenesWorldFactory(staticContainer.SingletonSharedDependencies, staticContainer.ECSWorldPlugins),
            };
        }
    }
}
