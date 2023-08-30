using Arch.Core;
using DCL.AvatarRendering.Wearables.Components;
using ECS.Abstract;

namespace DCL.AvatarRendering.Wearables
{
    public static class WorldExtensions
    {
        private static readonly QueryDescription WEARABLE_CATALOG = new QueryDescription().WithAll<WearableCatalog>();

        public static SingleInstanceEntity CacheWearableCatalog(this World world) =>
            new (in WEARABLE_CATALOG, world);

        public static ref readonly WearableCatalog GetWearableCatalog(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<WearableCatalog>(instance);
    }
}
