using Arch.Core;
using ECS.Abstract;

namespace DCL.Optimization.AdaptivePerformance.Components
{
    public static class WorldExtensions
    {
        private static readonly QueryDescription AVATAR_VISIBILITY_CONFIG_QUERY = new QueryDescription().WithAll<AvatarVisibilityConfigComponent>();

        /// <summary>
        /// Caches the singleton entity containing AvatarVisibilityConfigComponent.
        /// The entity must already exist in the world.
        /// </summary>
        public static SingleInstanceEntity CacheAvatarVisibilityConfig(this World world) =>
            new (in AVATAR_VISIBILITY_CONFIG_QUERY, world);

        /// <summary>
        /// Gets a reference to the AvatarVisibilityConfigComponent from the singleton entity.
        /// </summary>
        public static ref AvatarVisibilityConfigComponent GetAvatarVisibilityConfig(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<AvatarVisibilityConfigComponent>(instance);
    }
}
