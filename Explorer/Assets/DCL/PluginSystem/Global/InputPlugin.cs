using Arch.SystemGroups;
using DCL.CharacterCamera.Systems;
using DCL.CharacterMotion.Systems;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using UnityEngine.EventSystems;

namespace DCL.PluginSystem.Global
{
    public class InputPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly DCLInput dclInput;

        public InputPlugin(DCLInput dclInput)
        {
            this.dclInput = dclInput;
            dclInput.Enable();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            builder.World.Create(new InputMapComponent((InputMapComponent.Kind)(~0)));

            ApplyInputMapsSystem.InjectToWorld(ref builder, dclInput);
            UpdateInputPhysicsTickSystem.InjectToWorld(ref builder);
            UpdateInputJumpSystem.InjectToWorld(ref builder, dclInput.Player.Jump);
            UpdateInputMovementSystem.InjectToWorld(ref builder, dclInput);
            UpdateCameraInputSystem.InjectToWorld(ref builder, dclInput);
            DropPlayerFromFreeCameraSystem.InjectToWorld(ref builder, dclInput.FreeCamera.DropPlayer);
            UpdateCursorInputSystem.InjectToWorld(ref builder, dclInput, new UIRaycaster(EventSystem.current), new DCLCursor());

            // UpdateInputButtonSystem<PrimaryKey>.InjectToWorld(ref builder, dclInput.Player.PrimaryKey);

        }
    }
}
