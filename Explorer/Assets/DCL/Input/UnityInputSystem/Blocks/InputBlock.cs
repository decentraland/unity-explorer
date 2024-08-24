using Arch.Core;
using DCL.Input.Component;
using DCL.Utilities;

namespace DCL.Input.UnityInputSystem.Blocks
{
    public class InputBlock : IInputBlock
    {
        private readonly ObjectProxy<World> globalWorld;

        public InputBlock(ObjectProxy<World> globalWorld)
        {
            this.globalWorld = globalWorld;
        }

        public void BlockMovement()
        {
            ref var inputMapComponent = ref globalWorld.StrictObject.CacheInputMap().GetInputMapComponent(globalWorld.StrictObject);
            inputMapComponent.BlockInput(InputMapComponent.Kind.Camera);
            inputMapComponent.BlockInput(InputMapComponent.Kind.Shortcuts);
            inputMapComponent.BlockInput(InputMapComponent.Kind.Player);
        }

        public void UnblockMovement()
        {
            ref var inputMapComponent = ref globalWorld.StrictObject.CacheInputMap().GetInputMapComponent(globalWorld.StrictObject);
            inputMapComponent.UnblockInput(InputMapComponent.Kind.Camera);
            inputMapComponent.UnblockInput(InputMapComponent.Kind.Shortcuts);
            inputMapComponent.UnblockInput(InputMapComponent.Kind.Player);
        }
    }
}
