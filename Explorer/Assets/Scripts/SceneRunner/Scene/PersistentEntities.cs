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

        //Root of the scene. Can be modified by the creator
        public readonly Entity SceneRoot;

        //Container of the root of the scene. Can only be modified by us
        public readonly Entity SceneContainer;


        public PersistentEntities(Entity player, Entity camera, Entity sceneRoot, Entity sceneContainer)
        {
            Player = player;
            Camera = camera;
            SceneRoot = sceneRoot;
            SceneContainer = sceneContainer;
        }
    }
}
