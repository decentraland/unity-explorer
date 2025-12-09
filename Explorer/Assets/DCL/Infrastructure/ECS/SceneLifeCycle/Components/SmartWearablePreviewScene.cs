using Arch.Core;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Attached to the realm entity.
    ///     Signals to the system that scene loading has started and stores a reference to the loaded scene entity.
    /// </summary>
    public struct SmartWearablePreviewScene
    {
        /// <summary>
        ///     The scene entity that was loaded.
        /// </summary>
        public Entity Value;
    }
}
