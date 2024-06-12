using Arch.Core;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.Mapping
{
    /// <summary>
    ///     None thread safe
    /// </summary>
    public class SceneMapping : ISceneMapping
    {
        private readonly Dictionary<string, World> worlds = new ();
        private readonly Dictionary<Vector2Int, World> worldsByCoordinates = new ();

        public World? GetWorld(string sceneName)
        {
            worlds.TryGetValue(sceneName, out var world);
            return world;
        }

        public World? GetWorld(Vector2Int coordinates)
        {
            worldsByCoordinates.TryGetValue(coordinates, out var world);
            return world;
        }

        public void Register(string sceneName, Vector2Int coordinates, World world)
        {
            worlds[sceneName] = world;
            worldsByCoordinates[coordinates] = world;
        }
    }
}
