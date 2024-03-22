using Arch.SystemGroups;
using DCL.AvatarRendering.Emotes;
using DCL.CharacterCamera.Systems;
using DCL.CharacterMotion.Systems;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using DCL.Multiplayer.Emotes;
using UnityEngine.EventSystems;

namespace DCL.PluginSystem.Global
{
    public class InputPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly DCLInput dclInput;
        private readonly IEmoteCache emoteCache;
        private readonly MultiplayerEmotesMessageBus messageBus;

        public InputPlugin(DCLInput dclInput, IEmoteCache emoteCache, MultiplayerEmotesMessageBus messageBus)
        {
            this.dclInput = dclInput;
            this.emoteCache = emoteCache;
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
            UpdateCursorInputSystem.InjectToWorld(ref builder, dclInput, new UnityEventSystem(EventSystem.current), new DCLCursor());
            UpdateEmoteInputSystem.InjectToWorld(ref builder, dclInput.Emotes, messageBus);
        }
    }
}
