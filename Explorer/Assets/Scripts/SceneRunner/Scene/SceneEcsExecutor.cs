using Arch.Core;
using Utility.Multithreading;

namespace SceneRunner.Scene
{
    /// <summary>
    ///     Provides an exposed way to write into ECS World from the global world
    /// </summary>
    public readonly struct SceneEcsExecutor
    {
        public SceneEcsExecutor(World world)
        {
            World = world;
        }

        /// <summary>
        ///     World must be accessed via Sync to keep state in sync with the state of the scene that is being updated from the background thread
        /// </summary>
        public readonly World World;
    }
}
