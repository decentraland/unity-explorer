using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.Wearables;
using DCL.PluginSystem.Global;
using ECS;
using ECS.Prioritization.Components;
using SceneRunner.EmptyScene;
using System.Collections.Generic;
using System.Linq;
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
            var realmData = new RealmData();

            var globalPlugins = new List<IDCLGlobalPlugin>
            {
                new CharacterMotionPlugin(staticContainer.AssetsProvisioner, staticContainer.CharacterObject),
                new InputPlugin(),
                new CharacterCameraPlugin(staticContainer.AssetsProvisioner, realmSamplingData, staticContainer.CameraSamplingData),
                new ProfilingPlugin(staticContainer.AssetsProvisioner, staticContainer.ProfilingProvider),

                //TODO: Remove this hardcoded url after the realm has been connected to get the catalyst url
                new WearablePlugin(realmData),
                new AvatarPlugin(staticContainer.SingletonSharedDependencies.FrameTimeCapBudgetProvider,
                    staticContainer.ComponentsContainer.ComponentPoolsRegistry.GetReferenceTypePool<AvatarBase>()),
            };

            globalPlugins.AddRange(staticContainer.SharedPlugins);

            return new DynamicWorldContainer
            {
                RealmController = new RealmController(sceneLoadRadius, staticLoadPositions, realmData),
                GlobalWorldFactory = new GlobalWorldFactory(in staticContainer, staticContainer.RealmPartitionSettings,
                    staticContainer.CameraSamplingData, realmSamplingData, globalPlugins),
                GlobalPlugins = globalPlugins.Concat(staticContainer.SharedPlugins).ToList(),
                EmptyScenesWorldFactory = new EmptyScenesWorldFactory(staticContainer.SingletonSharedDependencies, staticContainer.ECSWorldPlugins),
            };
        }
    }
}
