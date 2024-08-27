using Arch.Core;
using DCL.Input.Component;
using DCL.Utilities;
using ECS.Abstract;
using Utility;

namespace DCL.Input.UnityInputSystem.Blocks
{
    public class InputBlock : IInputBlock
    {
        private readonly ObjectProxy<World> globalWorld;
        private SingleInstanceEntity inputMap;

        public InputBlock(ObjectProxy<World> globalWorld)
        {
            this.globalWorld = globalWorld;
        }

        public void Initialize()
        {
            inputMap = globalWorld.StrictObject.CacheInputMap();
        }

        public void BlockInputs(params InputMapComponent.Kind[] kinds)
        {
            ref var inputMapComponent = ref inputMap.GetInputMapComponent(globalWorld.StrictObject);

            foreach (var kind in kinds)
            {
                inputMapComponent.BlockInput(kind);
            }
        }

        public void UnblockInputs(params InputMapComponent.Kind[] kinds)
        {
            ref var inputMapComponent = ref inputMap.GetInputMapComponent(globalWorld.StrictObject);

            foreach (var kind in kinds)
            {
                inputMapComponent.UnblockInput(kind);
            }
        }
    }
}
