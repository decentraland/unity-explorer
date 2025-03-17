using System;

namespace ECS.SceneLifeCycle.Components
{
    /// <summary>
    ///     Does not exist for empty scenes
    /// </summary>
    public struct VisualSceneState
    {
        public VisualSceneStateEnum CurrentVisualSceneState;
        public bool IsDirty;
    }

    [Flags]
    public enum VisualSceneStateEnum
    {
        UNINITIALIZED = 0,
        SHOWING_LOD = 1 << 0, // 1
        ROAD = 1 << 1, // 2
        SHOWING_SCENE = 1 << 2, // 4
        // Add more states as needed
    }
}
