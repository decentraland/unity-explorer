namespace DCL.Profiling
{
    /// <summary>
    ///     Per-scene runtime metrics. Written from the scene background thread (tick loop and
    ///     <see cref="SceneRuntime.Apis.Modules.EngineApi.EngineApiWrapper" /> callbacks); read from
    ///     the Unity main thread by debug systems.
    /// </summary>
    public sealed class SceneRuntimeMetrics
    {
        public readonly SampledCounter TickTimesNs = new ();
        public readonly SampledCounter BytesFromScene = new ();
        public readonly SampledCounter BytesToScene = new ();
        public readonly SampledCounter MessagesFromScene = new ();
        public readonly SampledCounter MessagesToScene = new ();

        public int TargetFps { get; set; }
    }
}
