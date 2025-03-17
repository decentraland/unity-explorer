using SceneRunner.Mapping;
using UnityEngine;

namespace SceneRunner.Debugging.Hub
{
    public class WorldInfoHub : IWorldInfoHub
    {
        private readonly IReadOnlySceneMapping mapping;

        public WorldInfoHub(IReadOnlySceneMapping mapping)
        {
            this.mapping = mapping;
        }

        public IWorldInfo? WorldInfo(string sceneName)
        {
            var world = mapping.GetWorld(sceneName);
            return world == null ? null : new WorldInfo(world);
        }

        public IWorldInfo? WorldInfo(Vector2Int coordinates)
        {
            var world = mapping.GetWorld(coordinates);
            return world == null ? null : new WorldInfo(world);
        }
    }
}
