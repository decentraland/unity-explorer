using Arch.Core;
using ECS.Abstract;
using ECS.Input.Component.Physics;

namespace ECS.Input
{
    public static class WorldExtensions
    {
        private static readonly QueryDescription QUERY = new QueryDescription().WithAll<PhysicsTickComponent>();

        public static SingleInstanceEntity CachePhysicsTick(this World world) =>
            new (in QUERY, world);

        public static ref readonly PhysicsTickComponent GetPhysicsTickComponent(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<PhysicsTickComponent>(instance);
    }
}
