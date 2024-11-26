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

    public enum VisualSceneStateEnum
    {
        UNINITIALIZED,
        SHOWING_SCENE,
        SHOWING_LOD,
        ROAD,
    }
}
