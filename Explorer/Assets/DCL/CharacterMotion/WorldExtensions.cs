using Arch.Core;
using DCL.Time.Components;
using ECS.Abstract;

namespace DCL.CharacterMotion
{
    public static class WorldExtensions
    {
        private static readonly QueryDescription PHYSICS_TICK_QUERY = new QueryDescription().WithAll<TimeComponent>();

        public static SingleInstanceEntity CacheTime(this World world) =>
            new (in PHYSICS_TICK_QUERY, world);

        public static ref readonly TimeComponent GetTimeComponent(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<TimeComponent>(instance);
    }
}
