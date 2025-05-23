using Utility.Multithreading;

namespace SceneRunner.Scene
{
    public interface ISceneStateProvider
    {
        /// <summary>
        ///     Is this scene the player's currently on?
        /// </summary>
        bool IsCurrent { get; set; }

        Atomic<SceneState> State { get; set; }

        uint TickNumber { get; set; }

        ref readonly SceneEngineStartInfo EngineStartInfo { get; }

        void SetRunning(SceneEngineStartInfo startInfo);
    }

    public static class SceneStateProviderExtensions
    {
        public static bool IsNotRunningState(this ISceneStateProvider sceneStateProvider) =>
            sceneStateProvider.State.Value()
                is SceneState.Disposing
                or SceneState.Disposed
                or SceneState.JavaScriptError
                or SceneState.EngineError;
    }
}
