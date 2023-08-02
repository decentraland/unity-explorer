using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera.Systems;
using ECS.CharacterMotion.Systems;
using ECS.Input.Systems.Physics;

namespace Global.Dynamic.Plugins
{
    public class InputPlugin : IECSGlobalPlugin
    {
        private readonly DCLInput dclInput;

        public InputPlugin()
        {
            dclInput = new DCLInput();
            dclInput.Enable();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            UpdateInputPhysicsTickSystem.InjectToWorld(ref builder);
            UpdateInputJumpSystem.InjectToWorld(ref builder, dclInput.Player.Jump);

            UpdateInputMovementSystem.InjectToWorld(ref builder, dclInput);
            UpdateCameraInputSystem.InjectToWorld(ref builder, dclInput);

            // UpdateInputButtonSystem<PrimaryKey>.InjectToWorld(ref builder, dclInput.Player.PrimaryKey);
        }
    }
}
