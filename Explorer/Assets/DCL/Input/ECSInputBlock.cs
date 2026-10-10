using System.Linq;
using Arch.Core;
using CodeLess.Interfaces;
using DCL.Input.Component;
using ECS.Abstract;

namespace DCL.Input
{
    [AutoInterface]
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

        public void EnableAll(params InputMapComponent.Kind[] except)
        {
            inputMap ??= globalWorld.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.Value.GetInputMapComponent(globalWorld);

            foreach (var kind in InputMapComponent.VALUES)
            {
                if (except != null && except.Contains(kind))
                    continue;

                inputMapComponent.UnblockInput(kind);
            }
        }
    }
}
