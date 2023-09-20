namespace SceneRunner.Scene
{
    /// <summary>
    ///     Information about the engine upon the scene launch
    /// </summary>
    public readonly struct SceneEngineStartInfo
    {
        public readonly float Time;
        public readonly int FrameNumber;

        public SceneEngineStartInfo(float time, int frameNumber)
        {
            Time = time;
            FrameNumber = frameNumber;
        }
    }
}
