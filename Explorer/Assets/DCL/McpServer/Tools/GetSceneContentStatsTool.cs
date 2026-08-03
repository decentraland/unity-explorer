using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.Profiling;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System.Globalization;
using System.Text;
using System.Threading;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Exposes the current scene's content stats — the same numbers as the "Current scene" debug
    ///     widget and the scene metrics panel — as structured JSON. Sets a demand flag so the scene
    ///     world runs a counting pass even while every stats UI is closed, then waits for it to complete.
    /// </summary>
    public class GetSceneContentStatsTool : McpTool
    {
        private const int POLL_INTERVAL_MS = 100;
        private const int DEFAULT_COLLECTION_TIMEOUT_MS = 3000;

        private readonly IScenesCache scenesCache;
        private readonly int collectionTimeoutMs;

        public override string Name => "get_scene_content_stats";

        public override string Description =>
            "Read the current scene's content statistics: entities, triangles, meshes (bodies), geometries, materials, textures, colliders and "
            + "external content (media streamed from outside the content server + NFT shapes). Metrics with a documented soft limit "
            + "(https://docs.decentraland.org/creator/scenes-sdk7/optimizing/scene-limitations/) also report the cap for the scene's parcel count; "
            + "exceeding a cap degrades performance but is not enforced. Triggers a fresh counting pass, so values reflect the currently rendered content.";

        public override JObject OutputSchema =>
            McpJsonSchema.Object()
                          .Integer("parcelCount")
                          .Boolean("fresh", "True when a counting pass completed during this call; false when the values are from an earlier pass.")
                          .Integer("entities")
                          .Integer("entitiesCap")
                          .Integer("triangles")
                          .Integer("trianglesCap")
                          .Integer("bodies", "Renderer count (primitive + GLTF).")
                          .Integer("bodiesCap")
                          .Integer("geometries", "Unique meshes. No documented cap.")
                          .Integer("materials", "Unique materials, SDK and GLTF-embedded.")
                          .Integer("materialsCap")
                          .Integer("textures", "Unique textures probed from common shader properties.")
                          .Integer("texturesCap")
                          .Integer("colliders", "Primitive + GLTF colliders. No documented cap.")
                          .Integer("externalContent", "Media streamed from outside the content server + NFT shapes. No documented cap.")
                          .Build();

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public GetSceneContentStatsTool(IScenesCache scenesCache, int collectionTimeoutMs = DEFAULT_COLLECTION_TIMEOUT_MS)
        {
            this.scenesCache = scenesCache;
            this.collectionTimeoutMs = collectionTimeoutMs;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (scene == null)
                return McpToolResult.Error("No scene is loaded at the player's current parcel.");

            SceneContentStats stats = scene.RuntimeMetrics.ContentStats;
            long collectionsBefore = stats.CollectionCount;
            stats.RequestedByMcp = true;

            try
            {
                var elapsedMs = 0;

                while (stats.CollectionCount == collectionsBefore && elapsedMs < collectionTimeoutMs)
                {
                    await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);
                    elapsedMs += POLL_INTERVAL_MS;

                    if (scenesCache.CurrentScene.Value != scene)
                        return McpToolResult.Error("The current scene changed while collecting stats.");
                }
            }
            finally
            {
                stats.RequestedByMcp = false;
            }

            if (!stats.HasData)
                return McpToolResult.Error("The scene world did not produce content stats in time. Is the scene running?");

            bool fresh = stats.CollectionCount != collectionsBefore;
            int parcelCount = scene.SceneData.Parcels.Count;
            SceneContentCaps caps = SceneContentCaps.ForParcelCount(parcelCount);

            var structured = new JObject
            {
                ["parcelCount"] = parcelCount,
                ["fresh"] = fresh,
                ["entities"] = stats.Entities,
                ["entitiesCap"] = caps.Entities,
                ["triangles"] = stats.Triangles,
                ["trianglesCap"] = caps.Triangles,
                ["bodies"] = stats.Bodies,
                ["bodiesCap"] = caps.Bodies,
                ["geometries"] = stats.Geometries,
                ["materials"] = stats.Materials,
                ["materialsCap"] = caps.Materials,
                ["textures"] = stats.Textures,
                ["texturesCap"] = caps.Textures,
                ["colliders"] = stats.Colliders,
                ["externalContent"] = stats.ExternalContent,
            };

            var text = new StringBuilder();
            text.Append("Scene content stats (").Append(parcelCount).Append(" parcels, ").Append(fresh ? "fresh" : "from an earlier pass").AppendLine("):");
            AppendCapped(text, "Entities", stats.Entities, caps.Entities);
            AppendCapped(text, "Triangles", stats.Triangles, caps.Triangles);
            AppendCapped(text, "Meshes (bodies)", stats.Bodies, caps.Bodies);
            AppendCount(text, "Geometries", stats.Geometries);
            AppendCapped(text, "Materials", stats.Materials, caps.Materials);
            AppendCapped(text, "Textures", stats.Textures, caps.Textures);
            AppendCount(text, "Colliders", stats.Colliders);
            AppendCount(text, "External content", stats.ExternalContent);

            return McpToolResult.TextWithStructured(text.ToString(), structured);
        }

        private static void AppendCapped(StringBuilder text, string title, long value, long cap)
        {
            if (cap <= 0)
            {
                AppendCount(text, title, value);
                return;
            }

            text.Append(title).Append(": ").Append(value.ToString("N0", CultureInfo.InvariantCulture))
                .Append(" / ").Append(cap.ToString("N0", CultureInfo.InvariantCulture))
                .Append(" (").Append((value * 100f / cap).ToString("F0", CultureInfo.InvariantCulture)).AppendLine("%)");
        }

        private static void AppendCount(StringBuilder text, string title, long value)
        {
            text.Append(title).Append(": ").AppendLine(value.ToString("N0", CultureInfo.InvariantCulture));
        }
    }
}
