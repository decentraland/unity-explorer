using DCL.Character;
using DCL.CharacterCamera.Components;
using DCL.CharacterMotion.Settings;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using Global.Dynamic.Plugins;
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

        public static DynamicWorldContainer Create(
            in StaticContainer staticContainer,
            IRealmPartitionSettings realmPartitionSettings,
            ICinemachinePreset cinemachinePreset,
            ICharacterControllerSettings characterControllerSettings,
            ICharacterObject characterObject,
            IReadOnlyList<int2> staticLoadPositions, int sceneLoadRadius)
        {
            var realmSamplingData = new RealmSamplingData();

            return new DynamicWorldContainer
            {
                RealmController = new RealmController(sceneLoadRadius, staticLoadPositions),
                GlobalWorldFactory = new GlobalWorldFactory(in staticContainer, realmPartitionSettings,
                    staticContainer.CameraSamplingData, realmSamplingData, new IECSGlobalPlugin[]
                    {
                        new CharacterMotionPlugin(characterControllerSettings, characterObject),
                        new InputPlugin(),
                        new CharacterCameraPlugin(cinemachinePreset, realmSamplingData, staticContainer.CameraSamplingData),
                    }),
                EmptyScenesWorldFactory = new EmptyScenesWorldFactory(staticContainer.SingletonSharedDependencies, staticContainer.ECSWorldPlugins),
            };
        }
    }
}
