using Arch.Core;
using DCL.Character.Components;
using ECS.Abstract;

namespace DCL.Character
{
    public static class WorldExtensions
    {
        private static readonly QueryDescription QUERY = new QueryDescription().WithAll<PlayerComponent>();

        public static SingleInstanceEntity CachePlayer(this World world) =>
            new (in QUERY, world);
    }
}
