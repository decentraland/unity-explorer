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

        /// <summary>
        ///     Per-message breakdown of the traffic the counters above only total; records only while a capture runs.
        /// </summary>
        public readonly CrdtTrafficProbe Traffic = new ();

        /// <summary>
        ///     Unlike the counters above, written and read on the Unity main thread only.
        /// </summary>
        public readonly SceneContentStats ContentStats = new ();

        public int TargetFps { get; set; }
    }
}
