using Arch.Core;
using DCL.Input.Component;
using DCL.Utilities;
using ECS.Abstract;

namespace DCL.Input
{
    public class ECSInputBlock : IInputBlock
    {
        private readonly World globalWorld;
        private SingleInstanceEntity? inputMap;

        public ECSInputBlock(World globalWorld)
        {
            this.globalWorld = globalWorld;
        }

        public void Disable(params InputMapComponent.Kind[] kinds)
        {
            inputMap ??= globalWorld.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.Value.GetInputMapComponent(globalWorld);

            foreach (var kind in kinds)
                inputMapComponent.BlockInput(kind);
        }

        public void Enable(params InputMapComponent.Kind[] kinds)
        {
            inputMap ??= globalWorld.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.Value.GetInputMapComponent(globalWorld);

            foreach (var kind in kinds)
                inputMapComponent.UnblockInput(kind);
        }
    }
}
