using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.Profiling;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
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
    ///     content-stats pass with a one-shot breakdown flag.
    /// </summary>
    public class GetSceneContentBreakdownTool : McpTool
    {
        private const int POLL_INTERVAL_MS = 100;
        private const int DEFAULT_COLLECTION_TIMEOUT_MS = 3000;
        private const int DEFAULT_LIMIT = 10;
        private const int MAX_LIMIT = 50;

        private readonly IScenesCache scenesCache;
        private readonly int collectionTimeoutMs;

        public override string Name => "get_scene_content_breakdown";

        public override string Description =>
            "Rank the current scene's rendered content by triangle count, grouped by source model: for each GLTF the summed triangles, "
            + "instance count and renderer count, plus one aggregate row for primitive meshes. Use it after get_scene_content_stats shows "
            + "a metric near its cap to find which assets to optimize. Triggers a fresh counting pass over the currently rendered content.";

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("limit", "Maximum entries to return, heaviest first. Default 10.");

        public GetSceneContentBreakdownTool(IScenesCache scenesCache, int collectionTimeoutMs = DEFAULT_COLLECTION_TIMEOUT_MS)
        {
            this.scenesCache = scenesCache;
            this.collectionTimeoutMs = collectionTimeoutMs;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            int limit = Mathf.Clamp(arguments.GetInt("limit", DEFAULT_LIMIT), 1, MAX_LIMIT);

            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (scene == null)
                return McpToolResult.Error("No scene is loaded at the player's current parcel.");

            SceneContentStats stats = scene.RuntimeMetrics.ContentStats;
            long collectionsBefore = stats.CollectionCount;
            stats.BreakdownRequested = true;
            stats.RequestedByMcp = true;

            try
            {
                var elapsedMs = 0;

                while (stats.CollectionCount == collectionsBefore && elapsedMs < collectionTimeoutMs)
                {
                    await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);
                    elapsedMs += POLL_INTERVAL_MS;

                    if (scenesCache.CurrentScene.Value != scene)
                        return McpToolResult.Error("The current scene changed while collecting the breakdown.");
                }
            }
            finally
            {
                stats.RequestedByMcp = false;
                stats.BreakdownRequested = false;
            }

            if (stats.CollectionCount == collectionsBefore)
                return McpToolResult.Error("The scene world did not produce a content breakdown in time. Is the scene running?");

            var sorted = new List<SceneContentBreakdownEntry>(stats.BreakdownEntries);
            sorted.Sort(static (a, b) => b.Triangles.CompareTo(a.Triangles));

            int returned = Mathf.Min(limit, sorted.Count);
            long sceneTriangles = stats.Triangles;

            var entries = new JArray();
            var text = new StringBuilder();
            text.Append("Heaviest content of ").Append(sorted.Count).Append(" sources (scene total ")
                .Append(sceneTriangles.ToString("N0", CultureInfo.InvariantCulture)).AppendLine(" triangles):");

            for (var i = 0; i < returned; i++)
            {
                SceneContentBreakdownEntry entry = sorted[i];
                float share = sceneTriangles > 0 ? entry.Triangles * 100f / sceneTriangles : 0f;

                entries.Add(new JObject
                {
                    ["source"] = entry.Source,
                    ["triangles"] = entry.Triangles,
                    ["trianglesSharePercent"] = Mathf.Round(share * 10f) / 10f,
                    ["instances"] = entry.Instances,
                    ["renderers"] = entry.Renderers,
                });

                text.Append(i + 1).Append(". ").Append(entry.Source)
                    .Append(" — ").Append(entry.Triangles.ToString("N0", CultureInfo.InvariantCulture))
                    .Append(" tris (").Append(share.ToString("F1", CultureInfo.InvariantCulture)).Append("% of scene, ")
                    .Append(entry.Instances).Append(entry.Instances == 1 ? " instance, " : " instances, ")
                    .Append(entry.Renderers).AppendLine(" renderers)");
            }

            if (returned < sorted.Count)
                text.Append(returned).Append(" of ").Append(sorted.Count).Append(" shown; raise limit (max ").Append(MAX_LIMIT).Append(") to see the rest.");

            var structured = new JObject
            {
                ["sceneTriangles"] = sceneTriangles,
                ["totalSources"] = sorted.Count,
                ["returned"] = returned,
                ["entries"] = entries,
            };

            return McpToolResult.TextWithStructured(text.ToString(), structured);
        }
    }
}
