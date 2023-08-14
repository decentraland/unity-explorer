using Arch.Core;

namespace DCL.PluginSystem.World
{
    /// <summary>
    ///     Entities that are created in a world factory and never destroyed while the ECS World is alive
    /// </summary>
    public readonly struct PersistentEntities
    {
        public readonly Entity SceneRoot;

        public PersistentEntities(Entity sceneRoot)
        {
            SceneRoot = sceneRoot;
        }
    }
}
