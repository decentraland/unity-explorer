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

        public void BlockInputs(InputMapComponent.Kind kinds, bool singleValue = false)
        {
            ref var inputMapComponent = ref inputMap.GetInputMapComponent(globalWorld.StrictObject);

            if (singleValue)
            {
                inputMapComponent.BlockInput(kinds);
                return;
            }

            for (var i = 0; i < InputMapComponent.VALUES.Count; i++)
            {
                InputMapComponent.Kind kind = InputMapComponent.VALUES[i];
                if (EnumUtils.HasFlag(kinds, kind)) { inputMapComponent.BlockInput(kind); }
            }
        }

        public void UnblockInputs(InputMapComponent.Kind kinds, bool singleValue = false)
        {
            ref var inputMapComponent = ref inputMap.GetInputMapComponent(globalWorld.StrictObject);

            if (singleValue)
            {
                inputMapComponent.UnblockInput(kinds);
                return;
            }

            for (var i = 0; i < InputMapComponent.VALUES.Count; i++)
            {
                InputMapComponent.Kind kind = InputMapComponent.VALUES[i];
                if (EnumUtils.HasFlag(kinds, kind)) { inputMapComponent.UnblockInput(kind); }
            }
        }
    }
}
