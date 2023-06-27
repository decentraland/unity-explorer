namespace SceneRunner.Scene
{
    public interface ISceneStateProvider
    {
        SceneState State { get; internal set; }
    }
}
