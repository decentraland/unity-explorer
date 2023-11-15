using Arch.Core;
using DCL.CharacterMotion.Settings;
using DCL.Time.Components;
using ECS.Abstract;

namespace DCL.CharacterMotion
{
    public static class WorldExtensions
    {
        private static readonly QueryDescription TIME_COMPONENT_QUERY = new QueryDescription().WithAll<TimeComponent>();
        private static readonly QueryDescription CHARACTER_SETTINGS_QUERY = new QueryDescription().WithAll<ICharacterControllerSettings>();

        private static readonly QueryDescription PHYSICS_TICK_QUERY = new QueryDescription().WithAll<PhysicsTickComponent>();

        public static SingleInstanceEntity CachePhysicsTick(this World world) =>
            new (in PHYSICS_TICK_QUERY, world);

        public static ref readonly PhysicsTickComponent GetPhysicsTickComponent(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<PhysicsTickComponent>(instance);


        public static SingleInstanceEntity CacheCharacterSettings(this World world) =>
            new (in CHARACTER_SETTINGS_QUERY, world);

        public static ref readonly ICharacterControllerSettings GetCharacterSettings(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<ICharacterControllerSettings>(instance);

        public static SingleInstanceEntity CacheTime(this World world) =>
            new (in TIME_COMPONENT_QUERY, world);

        public static ref readonly TimeComponent GetTimeComponent(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<TimeComponent>(instance);
    }
}
