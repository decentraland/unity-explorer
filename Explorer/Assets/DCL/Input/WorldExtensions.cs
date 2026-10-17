using Arch.Core;
using DCL.Input.Component;
using DCL.Time.Components;
using ECS.Abstract;

namespace DCL.Input
{
    public static class WorldExtensions
    {
        private static readonly QueryDescription PHYSICS_TICK_QUERY = new QueryDescription().WithAll<PhysicsTickComponent>();

        private static readonly QueryDescription INPUT_MAP_QUERY = new QueryDescription().WithAll<InputMapComponent>();

        public static SingleInstanceEntity CachePhysicsTick(this World world) =>
            new (in PHYSICS_TICK_QUERY, world);

        public static ref readonly PhysicsTickComponent GetPhysicsTickComponent(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<PhysicsTickComponent>(instance);

        public static SingleInstanceEntity CacheInputMap(this World world) =>
            new (in INPUT_MAP_QUERY, world);

        public static ref InputMapComponent GetInputMapComponent(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<InputMapComponent>(instance);
    }
}
