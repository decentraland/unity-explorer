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
        ///     While false the scene world skips collection entirely, so the counters cost nothing
        ///     when the debug panel is disabled or the widget is collapsed.
        /// </summary>
        public bool CollectionRequested;

        /// <summary>
        ///     False until the first collection pass completes for this scene.
        /// </summary>
        public bool HasData;

        public int Entities;
        public long Triangles;
        public int Bodies;
        public int Geometries;
        public int Materials;
        public int Textures;
        public int Colliders;
        public long ContentSizeBytes;
        public int ExternalContent;
    }
}
