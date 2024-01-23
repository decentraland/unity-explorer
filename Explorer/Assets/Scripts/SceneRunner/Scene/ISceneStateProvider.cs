namespace SceneRunner.Scene
{
    public interface ISceneStateProvider
    {
        /// <summary>
        ///     Is this scene the player's currently on?
        /// </summary>
        bool IsCurrent { get; set; }

        SceneState State { get; set; }

        uint TickNumber { get; set; }

        ref readonly SceneEngineStartInfo EngineStartInfo { get; }

        void SetRunning(SceneEngineStartInfo startInfo);
    }
}
