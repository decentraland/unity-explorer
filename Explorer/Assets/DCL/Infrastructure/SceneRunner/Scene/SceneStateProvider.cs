using Utility.Multithreading;

namespace SceneRunner.Scene
{
    public class SceneStateProvider : ISceneStateProvider
    {
        private SceneEngineStartInfo engineStartInfo;

        /// <summary>
        ///     <inheritdoc cref="ISceneStateProvider.IsCurrent" />
        /// </summary>
        public bool IsCurrent { get; set; }

        public Atomic<SceneState> State { get; set; } = new (SceneState.NotStarted);

        public uint TickNumber { get; set; }

        public ref readonly SceneEngineStartInfo EngineStartInfo => ref engineStartInfo;

        public void SetRunning(SceneEngineStartInfo startInfo)
        {
            State.Set(SceneState.Running);
            engineStartInfo = startInfo;
            TickNumber = 0;
        }
    }
}
