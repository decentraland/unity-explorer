using Arch.Core;
using DCL.Input.Component;
using ECS.Abstract;

namespace DCL.Input
{
    public class ECSInputGroupToggle : IInputGroupToggle
    {
        private readonly World world;
        private SingleInstanceEntity? inputEntity;

        public ECSInputGroupToggle(World world)
        {
            this.world = world;
        }

        public void Set(InputMapKind kind)
        {
            inputEntity ??= world.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputEntity.Value.GetInputMapComponent(world);
            inputMapComponent.Active = kind;
        }

        public void Enable(InputMapKind kind)
        {
            inputEntity ??= world.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputEntity.Value.GetInputMapComponent(world);
            inputMapComponent.Active |= kind;
        }

        public void Disable(InputMapKind kind)
        {
            inputEntity ??= world.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputEntity.Value.GetInputMapComponent(world);
            inputMapComponent.Active &= ~kind;
        }
    }
}
