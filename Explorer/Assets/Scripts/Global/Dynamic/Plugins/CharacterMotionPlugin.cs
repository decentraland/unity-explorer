using Arch.Core;
using Arch.SystemGroups;
using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
using ECS.CharacterMotion.Systems;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace Global.Dynamic.Plugins
{
    public class CharacterMotionPlugin : IECSGlobalPlugin
    {
        private readonly ICharacterControllerSettings settings;

        public CharacterMotionPlugin(ICharacterControllerSettings settings)
        {
            this.settings = settings;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            World world = builder.World;
            Vector3 playerPosition = world.Get<TransformComponent>(arguments.PlayerEntity).Transform.position;

            // Add Motion components
            world.Add(arguments.PlayerEntity,
                new CharacterPhysics(),
                new CharacterRigidTransform
                {
                    PreviousTargetPosition = playerPosition,
                    TargetPosition = playerPosition,
                },
                settings);

            InterpolateCharacterSystem.InjectToWorld(ref builder);
            RotateCharacterSystem.InjectToWorld(ref builder);
            CalculateCharacterVelocitySystem.InjectToWorld(ref builder);
        }
    }
}
