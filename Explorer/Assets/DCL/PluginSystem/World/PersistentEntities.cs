using Arch.Core;

namespace DCL.PluginSystem.World
{
    /// <summary>
    ///     Entities that are created in a world factory and never destroyed while the ECS World is alive
    /// </summary>
    public struct PersistentEntities
    {
        public Entity Player { get; private set; }
        public Entity Camera { get; private set; }
        public Entity SceneRoot { get; private set; }

        public void Setup(Entity sceneRootEntity, Entity playerEntity, Entity cameraEntity)
        {
            Camera = cameraEntity;
            Player = playerEntity;
            SceneRoot = sceneRootEntity;
        }
    }
}
