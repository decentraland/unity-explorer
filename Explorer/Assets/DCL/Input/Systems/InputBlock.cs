using Arch.Core;
using DCL.SDKComponents.InputModifier.Components;
using DCL.Utilities;

namespace DCL.Input.UnityInputSystem.Blocks
{
    public class InputBlock : IInputBlock
    {
        private readonly ObjectProxy<DCLInput> dclInput;
        private readonly ObjectProxy<World> globalWorld;
        private readonly ObjectProxy<Entity> playerEntity;

        public InputBlock(ObjectProxy<DCLInput> dclInput, ObjectProxy<World> globalWorld, ObjectProxy<Entity> playerEntity)
        {
            this.dclInput = dclInput;
            this.globalWorld = globalWorld;
            this.playerEntity = playerEntity;
        }

        public void BlockMovement()
        {
            ref var inputModifier = ref globalWorld.StrictObject.Get<InputModifierComponent>(playerEntity.StrictObject);
            inputModifier.DisableAll = true;
            dclInput.StrictObject.Shortcuts.Disable();
            dclInput.StrictObject.Camera.Disable();
            dclInput.StrictObject.Player.Disable();
        }

        public void UnblockMovement()
        {
            ref var inputModifier = ref globalWorld.StrictObject.Get<InputModifierComponent>(playerEntity.StrictObject);
            inputModifier.DisableAll = false;
            dclInput.StrictObject.Shortcuts.Enable();
            dclInput.StrictObject.Camera.Enable();
            dclInput.StrictObject.Player.Enable();
        }
    }
}
