using Arch.Core;
using Arch.SystemGroups;
using DCL.Character;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using UnityEngine;

namespace Global.Dynamic.Plugins
{
    public class CharacterMotionPlugin : IECSGlobalPlugin
    {
        private readonly ICharacterControllerSettings settings;
        private readonly ICharacterObject characterObject;

        public CharacterMotionPlugin(ICharacterControllerSettings settings,
            ICharacterObject characterObject)
        {
            this.settings = settings;
            this.characterObject = characterObject;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            World world = builder.World;
            Vector3 playerPosition = characterObject.Transform.position;

            // Add Motion components
            world.Add(arguments.PlayerEntity,
                new CharacterRigidTransform(),
                settings,
                characterObject.Controller);

            InterpolateCharacterSystem.InjectToWorld(ref builder);
            RotateCharacterSystem.InjectToWorld(ref builder);
            CalculateCharacterVelocitySystem.InjectToWorld(ref builder);
        }
    }
}
