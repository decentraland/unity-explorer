using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.Profiling;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Ranks the current scene's rendered content by source model so an agent can tell a creator
    ///     what to optimize, not just that a budget is exceeded. Piggybacks on the scene world's
    ///     content-stats pass with a breakdown demand refcount held while it waits.
    /// </summary>
    public class GetSceneContentBreakdownTool : McpTool
    {
        private const int DEFAULT_LIMIT = 10;
        private const int MAX_LIMIT = 50;

        private readonly IScenesCache scenesCache;
        private readonly int collectionTimeoutMs;
        private readonly SceneContentStatsPolling.WaitForCollection waitForCollection;

        public override string Name => "get_scene_content_breakdown";

        public override string Description =>
            "Rank the current scene's rendered content grouped by source model: for each GLTF the summed triangles, instance count, renderer "
            + "count, unique materials (a material shared by two sources counts once per source), shader variants and a draw-call estimate "
            + "(material slots across renderers, before batching), plus one aggregate row for primitive meshes. Each entry also reports its "
            + "visible subset — renderers that passed culling for the current point of view (per Renderer.isVisible, shadow casters included) "
            + "with their triangles and draw calls — so sortBy=visibleTriangles answers what THIS viewpoint pays for; position the camera first "
            + "(move_to/set_camera_pose). Use it after get_scene_content_stats shows a metric near its cap to find which assets to optimize. "
            + "Interpretation: URP's SRP Batcher bins draws by shader variant, so many materials sharing few shaderVariants render cheaply — "
            + "a high material count mainly costs memory, textures and lost instancing opportunities. Check shaderVariants before recommending "
            + "material dedup as a frame-time optimization. Triggers a fresh counting pass over the currently rendered content.";

        public override JObject OutputSchema =>
            McpJsonSchema.Object()
                          .Integer("sceneTriangles")
                          .Integer("sceneMaterials", "Unique materials scene-wide; entries can sum above this because a shared material counts once per source.")
                          .Integer("sceneShaderVariants")
                          .Integer("sceneDrawCallsEstimate", "Material slots summed across all sources' renderers, before batching.")
                          .Integer("visibleTriangles", "Triangles of renderers that passed culling for the current point of view, summed across all sources.")
                          .Integer("visibleDrawCallsEstimate")
                          .String("sortedBy")
                          .Integer("totalSources")
                          .Integer("returned", "Entries included below after applying limit.")
                          .ObjectArray("entries", McpJsonSchema.Object()
                                                                .String("source")
                                                                .Integer("triangles")
                                                                .Number("trianglesSharePercent")
                                                                .Integer("instances")
                                                                .Integer("renderers")
                                                                .Integer("materials")
                                                                .Integer("shaderVariants")
                                                                .Integer("drawCallsEstimate")
                                                                .Integer("visibleTriangles")
                                                                .Number("visibleTrianglesSharePercent")
                                                                .Integer("visibleRenderers")
                                                                .Integer("visibleDrawCallsEstimate"),
                              "Heaviest sources first, per sortBy.")
                          .Build();

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("limit", "Maximum entries to return, heaviest first. Default 10.")
                  .String("sortBy", "Metric to rank by. Default triangles.", enumValues: new[] { "triangles", "materials", "shaderVariants", "drawCalls", "visibleTriangles" });

        public GetSceneContentBreakdownTool(IScenesCache scenesCache, int collectionTimeoutMs = SceneContentStatsPolling.DEFAULT_COLLECTION_TIMEOUT_MS,
            SceneContentStatsPolling.WaitForCollection? waitForCollection = null)
        {
            this.scenesCache = scenesCache;
            this.collectionTimeoutMs = collectionTimeoutMs;
            this.waitForCollection = waitForCollection ?? SceneContentStatsPolling.WaitForCollectionAsync;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            int limit = Mathf.Clamp(arguments.GetInt("limit", DEFAULT_LIMIT), 1, MAX_LIMIT);
            string sortBy = arguments.GetString("sortBy", "triangles");

            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (scene == null)
                return McpToolResult.Error("No scene is loaded at the player's current parcel.");

            SceneContentStats stats = scene.RuntimeMetrics.ContentStats;
            long collectionsBefore = stats.CollectionCount;
            stats.BreakdownRequests++;
            stats.McpRequests++;

            try
            {
                if (!await waitForCollection(scenesCache, scene, stats, collectionsBefore, collectionTimeoutMs, ct))
                    return McpToolResult.Error("The current scene changed while collecting the breakdown.");
            }
            finally
            {
                stats.McpRequests--;
                stats.BreakdownRequests--;
            }

            if (stats.CollectionCount == collectionsBefore)
                return McpToolResult.Error("The scene world did not produce a content breakdown in time. Is the scene running?");

            var sorted = new List<SceneContentBreakdownEntry>(stats.BreakdownEntries);

            Comparison<SceneContentBreakdownEntry> comparison = sortBy switch
                                                                {
                                                                    "materials" => static (a, b) => b.Materials.CompareTo(a.Materials),
                                                                    "shaderVariants" => static (a, b) => b.ShaderVariants.CompareTo(a.ShaderVariants),
                                                                    "drawCalls" => static (a, b) => b.DrawCalls.CompareTo(a.DrawCalls),
                                                                    "visibleTriangles" => static (a, b) => b.VisibleTriangles.CompareTo(a.VisibleTriangles),
                                                                    _ => static (a, b) => b.Triangles.CompareTo(a.Triangles),
                                                                };

            sorted.Sort(comparison);

            int returned = Mathf.Min(limit, sorted.Count);
            long sceneTriangles = stats.Triangles;
            var sceneDrawCalls = 0;
            long visibleTriangles = 0;
            var visibleDrawCalls = 0;

            for (var i = 0; i < sorted.Count; i++)
            {
                sceneDrawCalls += sorted[i].DrawCalls;
                visibleTriangles += sorted[i].VisibleTriangles;
                visibleDrawCalls += sorted[i].VisibleDrawCalls;
            }

            var entries = new JArray();
            var text = new StringBuilder();
            text.Append("Heaviest content of ").Append(sorted.Count).Append(" sources by ").Append(sortBy)
                .Append(" (scene totals: ").Append(sceneTriangles.ToString("N0", CultureInfo.InvariantCulture)).Append(" triangles, ")
                .Append(stats.Materials.ToString("N0", CultureInfo.InvariantCulture)).Append(" unique materials across ")
                .Append(stats.ShaderVariants.ToString("N0", CultureInfo.InvariantCulture)).Append(" shader variants, ~")
                .Append(sceneDrawCalls.ToString("N0", CultureInfo.InvariantCulture)).Append(" draw calls; visible from this POV: ")
                .Append(visibleTriangles.ToString("N0", CultureInfo.InvariantCulture)).Append(" triangles, ~")
                .Append(visibleDrawCalls.ToString("N0", CultureInfo.InvariantCulture)).AppendLine(" draw calls):");

            for (var i = 0; i < returned; i++)
            {
                SceneContentBreakdownEntry entry = sorted[i];
                float share = sceneTriangles > 0 ? entry.Triangles * 100f / sceneTriangles : 0f;
                float visibleShare = visibleTriangles > 0 ? entry.VisibleTriangles * 100f / visibleTriangles : 0f;

                entries.Add(new JObject
                {
                    ["source"] = entry.Source,
                    ["triangles"] = entry.Triangles,
                    ["trianglesSharePercent"] = Mathf.Round(share * 10f) / 10f,
                    ["instances"] = entry.Instances,
                    ["renderers"] = entry.Renderers,
                    ["materials"] = entry.Materials,
                    ["shaderVariants"] = entry.ShaderVariants,
                    ["drawCallsEstimate"] = entry.DrawCalls,
                    ["visibleTriangles"] = entry.VisibleTriangles,
                    ["visibleTrianglesSharePercent"] = Mathf.Round(visibleShare * 10f) / 10f,
                    ["visibleRenderers"] = entry.VisibleRenderers,
                    ["visibleDrawCallsEstimate"] = entry.VisibleDrawCalls,
                });

                text.Append(i + 1).Append(". ").Append(entry.Source)
                    .Append(" — ").Append(entry.Triangles.ToString("N0", CultureInfo.InvariantCulture))
                    .Append(" tris (").Append(share.ToString("F1", CultureInfo.InvariantCulture)).Append("% of scene), ")
                    .Append(entry.Materials).Append(" materials (").Append(entry.ShaderVariants).Append(entry.ShaderVariants == 1 ? " shader variant), ~" : " shader variants), ~")
                    .Append(entry.DrawCalls).Append(" draw calls, ")
                    .Append(entry.Instances).Append(entry.Instances == 1 ? " instance, " : " instances, ")
                    .Append(entry.Renderers).Append(" renderers; visible: ")
                    .Append(entry.VisibleTriangles.ToString("N0", CultureInfo.InvariantCulture)).Append(" tris (")
                    .Append(visibleShare.ToString("F1", CultureInfo.InvariantCulture)).Append("% of view) in ")
                    .Append(entry.VisibleRenderers).Append(" renderers, ~")
                    .Append(entry.VisibleDrawCalls).AppendLine(" draw calls");
            }

            if (returned < sorted.Count)
                text.Append(returned).Append(" of ").Append(sorted.Count).Append(" shown; raise limit (max ").Append(MAX_LIMIT).Append(") to see the rest.");

            var structured = new JObject
            {
                ["sceneTriangles"] = sceneTriangles,
                ["sceneMaterials"] = stats.Materials,
                ["sceneShaderVariants"] = stats.ShaderVariants,
                ["sceneDrawCallsEstimate"] = sceneDrawCalls,
                ["visibleTriangles"] = visibleTriangles,
                ["visibleDrawCallsEstimate"] = visibleDrawCalls,
                ["sortedBy"] = sortBy,
                ["totalSources"] = sorted.Count,
                ["returned"] = returned,
                ["entries"] = entries,
            };

            return McpToolResult.TextWithStructured(text.ToString(), structured);
        }
    }
}
