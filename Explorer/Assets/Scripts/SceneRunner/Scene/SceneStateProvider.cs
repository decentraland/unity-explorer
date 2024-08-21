namespace SceneRunner.Scene
{
    public partial class SceneStateProvider : ISceneStateProvider
    {
        private SceneEngineStartInfo engineStartInfo;

        /// <summary>
        ///     <inheritdoc cref="ISceneStateProvider.IsCurrent" />
        /// </summary>
        public bool IsCurrent { get; set; }

        public SceneState State { get; set; } = SceneState.NotStarted;

        public uint TickNumber { get; set; }

        public ref readonly SceneEngineStartInfo EngineStartInfo => ref engineStartInfo;

        public void SetRunning(SceneEngineStartInfo startInfo)
        {
            State = SceneState.Running;
            engineStartInfo = startInfo;
            TickNumber = 0;
        }
    }
}
