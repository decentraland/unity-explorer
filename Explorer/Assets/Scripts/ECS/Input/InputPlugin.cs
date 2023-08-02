using Arch.Core;
using Arch.SystemGroups;
using ECS.Input.Component;
using ECS.Input.Systems;
using ECS.Input.Systems.Physics;

namespace ECS.Input
{
    public class InputPlugin
    {

        private DCLInput dclInput;
        public InputPlugin()
        {
            dclInput = new DCLInput();
            dclInput.Enable();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder)
        {
            UpdateInputPhysicsTickSystem.InjectToWorld(ref builder);
            UpdateInputJumpSystem.InjectToWorld(ref builder, dclInput.Player.Jump);

            UpdateInputMovementSystem.InjectToWorld(ref builder, dclInput);
            UpdateInputCameraZoomSystem.InjectToWorld(ref builder, dclInput);

            UpdateInputButtonSystem<PrimaryKey>.InjectToWorld(ref builder, dclInput.Player.PrimaryKey);
        }

    }
}
