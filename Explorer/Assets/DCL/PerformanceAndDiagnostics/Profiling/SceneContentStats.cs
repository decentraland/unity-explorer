using System.Collections.Generic;

namespace DCL.Profiling
{
    /// <summary>
    ///     One scene model's share of the rendered content, grouped by source. Produced on demand by
    ///     <c>SceneContentStatsSystem</c> while <see cref="SceneContentStats.BreakdownRequests" /> is held.
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

        /// <summary>
        ///     Unique shader variants (shader + enabled local keywords) across this source's materials —
        ///     the bins the SRP Batcher batches draws by. Few variants across many materials means the
        ///     materials render cheaply despite their count.
        /// </summary>
        public int ShaderVariants;

        /// <summary>
        ///     Subset of <see cref="Renderers" /> that passed culling for at least one active camera
        ///     (including shadow casting) during the collection pass, per <c>Renderer.isVisible</c>.
        /// </summary>
        public int VisibleRenderers;

        /// <summary>
        ///     Triangles of the visible renderers only — what the current point of view pays for.
        /// </summary>
        public long VisibleTriangles;

        /// <summary>
        ///     Material slots of the visible renderers only.
        /// </summary>
        public int VisibleDrawCalls;
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
        ///     Count of MCP tool calls currently waiting for a collection pass. A refcount rather than
        ///     a bool because tool calls can overlap and the first to finish must not cancel the
        ///     others' demand. Mutated on the Unity main thread only.
        /// </summary>
        public int McpRequests;

        /// <summary>
        ///     Count of waiters that need collection passes to also fill <see cref="BreakdownEntries" />.
        ///     Same refcount semantics as <see cref="McpRequests" />; each waiter releases its own count.
        ///     Collection must also be requested by a consumer flag.
        /// </summary>
        public int BreakdownRequests;

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
        ///     with <see cref="BreakdownRequests" /> held.
        /// </summary>
        public readonly List<SceneContentBreakdownEntry> BreakdownEntries = new ();

        /// <summary>
        ///     While false the scene world skips collection entirely, so the counters cost nothing
        ///     when no consumer is showing them.
        /// </summary>
        public bool CollectionRequested => RequestedByDebugWidget || RequestedByMetricsPanel || McpRequests > 0;

        public int Entities;
        public long Triangles;
        public int Bodies;
        public int Geometries;
        public int Materials;
        public int Textures;
        public int Colliders;

        /// <summary>
        ///     Media players in the scene — one per <c>VideoPlayer</c> / <c>AudioStream</c> component,
        ///     regardless of source. No documented cap.
        /// </summary>
        public int Videos;

        /// <summary>
        ///     Unique shader variants (shader + enabled local keywords) across all counted materials —
        ///     the bins the SRP Batcher batches draws by, so per-frame draw-call cost tracks this
        ///     number rather than <see cref="Materials" />.
        /// </summary>
        public int ShaderVariants;
    }
}
