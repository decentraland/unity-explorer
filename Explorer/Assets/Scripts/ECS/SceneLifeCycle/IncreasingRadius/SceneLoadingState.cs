namespace ECS.SceneLifeCycle.IncreasingRadius
{
    public class SceneLoadingState
    {
        public bool Loaded;
        public bool FullQuality;
        public VisualSceneStateEnum VisualSceneState;
    }

    public enum VisualSceneStateEnum
    {
        UNINITIALIZED,
        SHOWING_SCENE,
        SHOWING_LOD,
    }
}
