using Arch.Core;

namespace DCL.PluginSystem.World
{
    /// <summary>
    ///     Entities that are created in a world factory and never destroyed while the ECS World is alive
    /// </summary>
    public struct PersistentEntities
    {
        public readonly Entity Player;
        public readonly Entity Camera;
        public readonly Entity SceneRoot;

        public PersistentEntities(Entity player, Entity camera, Entity sceneRoot)
        {
            Player = player;
            Camera = camera;
            SceneRoot = sceneRoot;
        }
    }
}
