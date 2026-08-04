using System.Collections.Generic;

namespace DCL.Profiling
{
    /// <summary>
    ///     One scene model's share of the rendered content, grouped by source. Produced on demand by
    ///     <c>SceneContentStatsSystem</c> when <see cref="SceneContentStats.BreakdownRequested" /> is set.
    /// </summary>
    public struct SceneContentBreakdownEntry
    {
        public string Source;
        public int Instances;
        public int Renderers;
        public long Triangles;

        /// <summary>
        ///     Unique materials used by this source's renderers. A material shared between two
        ///     sources counts once per source, so entries can sum above the scene-wide unique total.
        /// </summary>
        public int Materials;

        /// <summary>
        ///     Material slots summed across this source's renderers — approximates the draw calls
        ///     the source costs before SRP batching and instancing.
        /// </summary>
        public int DrawCalls;
    }

    /// <summary>
    ///     Per-scene content statistics for the "Scene content" debug widget. Written by
    ///     <c>SceneContentStatsSystem</c> in the scene world and read by <c>DebugViewCurrentSceneSystem</c>
    ///     in the global world; both run on the Unity main thread.
    /// </summary>
    public sealed class SceneContentStats
    {
        /// <summary>
        ///     Set by the "Scene content" debug widget while it is expanded on this scene.
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
        ///     One-shot: when set, the next collection pass also fills <see cref="BreakdownEntries" />
        ///     and clears the flag. Collection must also be requested by a consumer flag.
        /// </summary>
        public bool BreakdownRequested;

        /// <summary>
        ///     False until the first collection pass completes for this scene.
        /// </summary>
        public bool HasData;

        /// <summary>
        ///     Incremented on every completed collection pass, letting consumers detect fresh data.
        /// </summary>
        public long CollectionCount;

        /// <summary>
        ///     Rendered content grouped by source model, unsorted. Only refreshed by passes that ran
        ///     with <see cref="BreakdownRequested" /> set.
        /// </summary>
        public readonly List<SceneContentBreakdownEntry> BreakdownEntries = new ();

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
