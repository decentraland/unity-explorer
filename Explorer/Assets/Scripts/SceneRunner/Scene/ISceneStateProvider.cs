namespace SceneRunner.Scene
{
    public interface ISceneStateProvider
    {
        SceneState State { get; set; }

        uint TickNumber { get; set; }

        ref readonly SceneEngineStartInfo EngineStartInfo { get; }

        void SetRunning(SceneEngineStartInfo startInfo);
    }
}
