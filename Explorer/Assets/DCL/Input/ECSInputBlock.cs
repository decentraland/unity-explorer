using Arch.Core;
using DCL.Input.Component;
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

        public void Disable(InputMapComponent.Kind kind)
        {
            inputMap ??= globalWorld.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.Value.GetInputMapComponent(globalWorld);
            inputMapComponent.BlockInput(kind);
        }

        public void Disable(InputMapComponent.Kind kind, InputMapComponent.Kind kind2)
        {
            inputMap ??= globalWorld.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.Value.GetInputMapComponent(globalWorld);
            inputMapComponent.BlockInput(kind);
            inputMapComponent.BlockInput(kind2);
        }

        public void Disable(InputMapComponent.Kind kind, InputMapComponent.Kind kind2, InputMapComponent.Kind kind3)
        {
            inputMap ??= globalWorld.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.Value.GetInputMapComponent(globalWorld);
            inputMapComponent.BlockInput(kind);
            inputMapComponent.BlockInput(kind2);
            inputMapComponent.BlockInput(kind3);
        }

        public void Enable(params InputMapComponent.Kind[] kinds)
        {
            inputMap ??= globalWorld.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.Value.GetInputMapComponent(globalWorld);

            foreach (var kind in kinds)
                inputMapComponent.UnblockInput(kind);
        }

        public void Enable(InputMapComponent.Kind kind)
        {
            inputMap ??= globalWorld.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.Value.GetInputMapComponent(globalWorld);
            inputMapComponent.UnblockInput(kind);
        }

        public void Enable(InputMapComponent.Kind kind, InputMapComponent.Kind kind2)
        {
            inputMap ??= globalWorld.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.Value.GetInputMapComponent(globalWorld);
            inputMapComponent.UnblockInput(kind);
            inputMapComponent.UnblockInput(kind2);
        }

        public void Enable(InputMapComponent.Kind kind, InputMapComponent.Kind kind2, InputMapComponent.Kind kind3)
        {
            inputMap ??= globalWorld.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.Value.GetInputMapComponent(globalWorld);
            inputMapComponent.UnblockInput(kind);
            inputMapComponent.UnblockInput(kind2);
            inputMapComponent.UnblockInput(kind3);
        }
    }
}
