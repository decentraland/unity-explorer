using Arch.Core;
using System;
using System.Collections.Generic;

namespace SceneRunner.Mapping
{
    /// <summary>
    ///     None thread safe
    /// </summary>
    public class SceneMapping : ISceneMapping
    {
        private readonly Dictionary<string, World> worlds = new ();

        public World? GetWorld(string sceneName)
        {
            worlds.TryGetValue(sceneName, out var world);
            return world;
        }

        public void Register(string sceneName, World world)
        {
            worlds[sceneName] = world;
        }
    }
}
