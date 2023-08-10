using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera.Systems;
using DCL.CharacterMotion.Systems;
using DCL.Input.Component;
using DCL.Input.Systems;

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
            builder.World.Create(new InputMapComponent((InputMapComponent.Kind)(~0)));

            ApplyInputMapsSystem.InjectToWorld(ref builder, dclInput);
            UpdateInputPhysicsTickSystem.InjectToWorld(ref builder);
            UpdateInputJumpSystem.InjectToWorld(ref builder, dclInput.Player.Jump);

            UpdateInputMovementSystem.InjectToWorld(ref builder, dclInput);
            UpdateCameraInputSystem.InjectToWorld(ref builder, dclInput);

            DropPlayerFromFreeCameraSystem.InjectToWorld(ref builder, dclInput.FreeCamera.DropPlayer);

            // UpdateInputButtonSystem<PrimaryKey>.InjectToWorld(ref builder, dclInput.Player.PrimaryKey);
        }
    }
}
