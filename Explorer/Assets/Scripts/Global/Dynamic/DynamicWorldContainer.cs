using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.Wearables;
using DCL.PluginSystem.Global;
using ECS.Prioritization.Components;
using SceneRunner.EmptyScene;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Global.Dynamic
{
    public class DynamicWorldContainer
    {
        public IRealmController RealmController { get; private set; }

        public GlobalWorldFactory GlobalWorldFactory { get; private set; }

        public EmptyScenesWorldFactory EmptyScenesWorldFactory { get; private set; }

        public IReadOnlyList<IDCLGlobalPlugin> GlobalPlugins { get; private set; }

        public static DynamicWorldContainer Create(
            in StaticContainer staticContainer,
            IReadOnlyList<int2> staticLoadPositions, int sceneLoadRadius)
        {
            var realmSamplingData = new RealmSamplingData();

            var globalPlugins = new IDCLGlobalPlugin[]
            {
                new CharacterMotionPlugin(staticContainer.AssetsProvisioner, staticContainer.CharacterObject),
                new InputPlugin(),
                new CharacterCameraPlugin(staticContainer.AssetsProvisioner, realmSamplingData, staticContainer.CameraSamplingData),
                new ProfilingPlugin(staticContainer.AssetsProvisioner, staticContainer.ProfilingProvider),

                //TODO: Remove this hardcoded url after the realm has been connected to get the catalyst url
                new WearablePlugin("https://peer.decentraland.org", "/content/entities/active/"),
                new AvatarPlugin(staticContainer.AssetsProvisioner, staticContainer.SingletonSharedDependencies.FrameTimeCapBudgetProvider,
                    staticContainer.ComponentsContainer.ComponentPoolsRegistry.GetReferenceTypePool<AvatarBase>(), "https://peer.decentraland.org", "/content/entities/active/"),
            };

            return new DynamicWorldContainer
            {
                RealmController = new RealmController(sceneLoadRadius, staticLoadPositions),
                GlobalWorldFactory = new GlobalWorldFactory(in staticContainer, staticContainer.RealmPartitionSettings,
                    staticContainer.CameraSamplingData, realmSamplingData, globalPlugins),
                GlobalPlugins = globalPlugins,
                EmptyScenesWorldFactory = new EmptyScenesWorldFactory(staticContainer.SingletonSharedDependencies, staticContainer.ECSWorldPlugins),
            };
        }
    }
}
