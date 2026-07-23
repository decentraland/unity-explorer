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

        /// <summary>
        ///     Tick at which the most recent non-hover pointer (down/up) result was written for this scene while
        ///     it was current. Zero means no user gesture has ever been recorded. Consumed by gesture-gated
        ///     restricted actions to reject calls that do not originate from recent user input.
        /// </summary>
        uint LastUserInputTick { get; set; }

        ref readonly SceneEngineStartInfo EngineStartInfo { get; }

        void Start(SceneEngineStartInfo startInfo);
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
