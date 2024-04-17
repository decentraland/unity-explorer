using Arch.SystemGroups;
using DCL.CharacterCamera.Systems;
using DCL.CharacterMotion.Systems;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using DCL.Multiplayer.Emotes.Interfaces;
using UpdateEmoteInputSystem = DCL.AvatarRendering.Emotes.UpdateEmoteInputSystem;

namespace DCL.PluginSystem.Global
{
    public class InputPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly DCLInput dclInput;
        private readonly IEmotesMessageBus messageBus;
        private readonly IEventSystem eventSystem;

        public InputPlugin(DCLInput dclInput, IEmotesMessageBus messageBus, IEventSystem eventSystem)
        {
            this.dclInput = dclInput;
            this.eventSystem = eventSystem;
            this.messageBus = messageBus;
            dclInput.Enable();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            builder.World.Create(new InputMapComponent((InputMapComponent.Kind)(~0)));

            ApplyInputMapsSystem.InjectToWorld(ref builder, dclInput);
            UpdateInputJumpSystem.InjectToWorld(ref builder, dclInput.Player.Jump);
            UpdateInputMovementSystem.InjectToWorld(ref builder, dclInput);
            UpdateCameraInputSystem.InjectToWorld(ref builder, dclInput);
            DropPlayerFromFreeCameraSystem.InjectToWorld(ref builder, dclInput.FreeCamera.DropPlayer);
            UpdateEmoteInputSystem.InjectToWorld(ref builder, dclInput.Emotes, messageBus);
            UpdateCursorInputSystem.InjectToWorld(ref builder, dclInput, eventSystem, new DCLCursor());

            // UpdateInputButtonSystem<PrimaryKey>.InjectToWorld(ref builder, dclInput.Player.PrimaryKey);
        }
    }
}
