using Arch.Core;
using DCL.CharacterMotion.Components;
using DCL.Utilities;
using System;

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
            globalWorld.StrictObject.AddOrGet(playerEntity.StrictObject, new MovementBlockerComponent());
            dclInput.StrictObject.Shortcuts.Disable();
            dclInput.StrictObject.Camera.Disable();
        }

        public void UnblockMovement()
        {
            globalWorld.StrictObject.Remove<MovementBlockerComponent>(playerEntity.StrictObject);
            dclInput.StrictObject.Shortcuts.Enable();
            dclInput.StrictObject.Camera.Enable();
        }
    }
}
