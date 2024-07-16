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
            World().AddOrGet(PlayerEntity(), new MovementBlockerComponent());
            DclInput().Shortcuts.Disable();
            DclInput().Camera.Disable();
        }

        public void UnblockMovement()
        {
            World().Remove<MovementBlockerComponent>(PlayerEntity());
            DclInput().Shortcuts.Enable();
            DclInput().Camera.Enable();
        }

        private World World()
        {
            if (globalWorld.Configured == false)
                throw new InvalidOperationException("World not configured");

            return globalWorld.Object!;
        }

        private DCLInput DclInput()
        {
            if (dclInput.Configured == false)
                throw new InvalidOperationException("World not configured");

            return dclInput.Object!;
        }

        private Entity PlayerEntity()
        {
            if (playerEntity.Configured == false)
                throw new InvalidOperationException("World not configured");

            return playerEntity.Object!;
        }
    }
}
