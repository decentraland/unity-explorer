using Arch.Core;
using DCL.Input.Component;
using DCL.Utilities;
using ECS.Abstract;

namespace DCL.Input
{
    public class ECSInputBlock : IInputBlock
    {
        private readonly ObjectProxy<World> globalWorld;
        private SingleInstanceEntity? inputMap;

        public ECSInputBlock(ObjectProxy<World> globalWorld)
        {
            this.globalWorld = globalWorld;
        }

        public void Disable(params InputMapComponent.Kind[] kinds)
        {
            inputMap ??= globalWorld.StrictObject.CacheInputMap();
            ref var inputMapComponent = ref inputMap.Value.GetInputMapComponent(globalWorld.StrictObject);

            foreach (var kind in kinds)
                inputMapComponent.BlockInput(kind);
        }

        public void Enable(params InputMapComponent.Kind[] kinds)
        {
            inputMap ??= globalWorld.StrictObject.CacheInputMap();
            ref var inputMapComponent = ref inputMap.Value.GetInputMapComponent(globalWorld.StrictObject);

            foreach (var kind in kinds)
                inputMapComponent.UnblockInput(kind);
        }
    }
}
