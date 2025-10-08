using System.Collections.Generic;
using Arch.Core;

namespace SceneRunner.Scene
{
    /// <summary>
    ///     Maps ECS World instances to their owning ISceneFacade for quick lookup from systems.
    /// </summary>
    public static class SceneWorldLocator
    {
        private static readonly Dictionary<World, ISceneFacade> map = new ();

        public static void Register(World world, ISceneFacade facade)
        {
            map[world] = facade;
        }

        public static void Unregister(World world)
        {
            map.Remove(world);
        }

        public static bool TryGet(World world, out ISceneFacade facade) =>
            map.TryGetValue(world, out facade);
    }
}
