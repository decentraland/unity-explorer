namespace DCL.Profiling
{
    /// <summary>
    ///     Per-scene content statistics for the "Current scene" debug widget. Written by
    ///     <c>SceneContentStatsSystem</c> in the scene world and read by <c>DebugViewCurrentSceneSystem</c>
    ///     in the global world; both run on the Unity main thread.
    /// </summary>
    public sealed class SceneContentStats
    {
        /// <summary>
        ///     Set by the "Current scene" debug widget while it is expanded on this scene.
        /// </summary>
        public bool RequestedByDebugWidget;

        /// <summary>
        ///     Set by the scene debug menu metrics panel while it is open on this scene.
        /// </summary>
        public bool RequestedByMetricsPanel;

        /// <summary>
        ///     Set by the MCP get_scene_content_stats tool while it waits for a collection pass.
        /// </summary>
        public bool RequestedByMcp;

        /// <summary>
        ///     False until the first collection pass completes for this scene.
        /// </summary>
        public bool HasData;

        /// <summary>
        ///     Incremented on every completed collection pass, letting consumers detect fresh data.
        /// </summary>
        public long CollectionCount;

        /// <summary>
        ///     While false the scene world skips collection entirely, so the counters cost nothing
        ///     when no consumer is showing them.
        /// </summary>
        public bool CollectionRequested => RequestedByDebugWidget || RequestedByMetricsPanel || RequestedByMcp;

        public int Entities;
        public long Triangles;
        public int Bodies;
        public int Geometries;
        public int Materials;
        public int Textures;
        public int Colliders;
        public int ExternalContent;
    }
}
