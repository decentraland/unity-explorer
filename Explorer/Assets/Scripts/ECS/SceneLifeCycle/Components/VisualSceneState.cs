namespace ECS.SceneLifeCycle.Components
{
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
        ROAD
    }

}
