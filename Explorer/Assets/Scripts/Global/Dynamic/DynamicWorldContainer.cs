using DCL.PluginSystem.Global;
using ECS.Prioritization.Components;
using SceneRunner.EmptyScene;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.UIElements;

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
            UIDocument rootUIDocument,
            IReadOnlyList<int2> staticLoadPositions, int sceneLoadRadius)
        {
            var realmSamplingData = new RealmSamplingData();
            var dclInput = new DCLInput();

            var globalPlugins = new IDCLGlobalPlugin[]
            {
                new CharacterMotionPlugin(staticContainer.AssetsProvisioner, staticContainer.CharacterObject),
                new InputPlugin(dclInput),
                new GlobalInteractionPlugin(dclInput, rootUIDocument, staticContainer.AssetsProvisioner, staticContainer.EntityCollidersGlobalCache),
                new CharacterCameraPlugin(staticContainer.AssetsProvisioner, realmSamplingData, staticContainer.CameraSamplingData),
                new ProfilingPlugin(staticContainer.AssetsProvisioner, staticContainer.ProfilingProvider)
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
