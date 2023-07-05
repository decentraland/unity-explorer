namespace SceneRunner.Scene
{
    public class SceneStateProvider : ISceneStateProvider
    {
        SceneState ISceneStateProvider.State { get; set; } = SceneState.NotStarted;
    }
}
