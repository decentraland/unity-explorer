using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.Profiling;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Samples the client's real frame rate over its own short window (no shared profiler state
    ///     is touched) and reports it together with the current scene's tick FPS, so an agent can
    ///     correlate a viewpoint's content cost with the frame rate it actually produces.
    /// </summary>
    public class GetPerformanceStatsTool : McpTool
    {
        private const float DEFAULT_SAMPLE_SECONDS = 2f;
        private const float MIN_SAMPLE_SECONDS = 0.5f;
        private const float MAX_SAMPLE_SECONDS = 10f;
        private const float HICCUP_THRESHOLD_MS = 50f;

        private readonly IScenesCache scenesCache;
        private readonly long[] tickScratch = new long[SampledCounter.BUFFER_CAPACITY];

        public override string Name => "get_performance_stats";

        public override string Description =>
            "Sample the client's real frame rate over a short window and report render FPS (average, min, max, hiccup frames > 50 ms) plus "
            + "the current scene's tick FPS vs its target. The call holds for sampleSeconds while it measures. Use together with "
            + "get_scene_content_breakdown (sortBy=visibleTriangles) to correlate a viewpoint's content cost with the frame rate it actually "
            + "produces — position the camera first, then sample.";

        public override JObject OutputSchema =>
            McpJsonSchema.Object()
                          .Number("sampleSeconds")
                          .Integer("framesSampled")
                          .Number("averageFps")
                          .Number("minFps", "Lowest instantaneous FPS in the window (longest frame).")
                          .Number("maxFps")
                          .Number("averageFrameMs")
                          .Number("maxFrameMs")
                          .Integer("hiccupFrames", "Frames longer than 50 ms in the window.")
                          .Object("sceneTick", McpJsonSchema.Object()
                                                             .Number("averageFps")
                                                             .Number("minFps")
                                                             .Number("maxFps")
                                                             .Integer("targetFps"),
                              "The current scene's JS tick rate, or null when no scene is loaded or it has not ticked yet.", nullable: true)
                          .Build();

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public GetPerformanceStatsTool(IScenesCache scenesCache)
        {
            this.scenesCache = scenesCache;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            float sampleSeconds = Mathf.Clamp(arguments.GetFloat("sampleSeconds", DEFAULT_SAMPLE_SECONDS), MIN_SAMPLE_SECONDS, MAX_SAMPLE_SECONDS);

            var framesSampled = 0;
            float totalMs = 0f;
            float minFrameMs = float.MaxValue;
            float maxFrameMs = 0f;
            var hiccupFrames = 0;

            float start = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - start < sampleSeconds)
            {
                await UniTask.NextFrame(ct);

                float frameMs = Time.unscaledDeltaTime * 1000f;
                framesSampled++;
                totalMs += frameMs;
                if (frameMs < minFrameMs) minFrameMs = frameMs;
                if (frameMs > maxFrameMs) maxFrameMs = frameMs;
                if (frameMs > HICCUP_THRESHOLD_MS) hiccupFrames++;
            }

            if (framesSampled == 0)
                return McpToolResult.Error("No frames rendered during the sampling window.");

            float averageFrameMs = totalMs / framesSampled;
            float averageFps = 1000f / averageFrameMs;
            float minFps = 1000f / maxFrameMs;
            float maxFps = 1000f / minFrameMs;

            JObject? sceneTick = null;
            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (scene != null)
            {
                SceneRuntimeMetrics metrics = scene.RuntimeMetrics;
                int tickSamples = metrics.TickTimesNs.CopySnapshot(tickScratch);

                if (tickSamples > 0)
                {
                    long totalNs = 0;
                    long minNs = long.MaxValue;
                    long maxNs = long.MinValue;

                    for (var i = 0; i < tickSamples; i++)
                    {
                        long ns = tickScratch[i];
                        if (ns <= 0) continue;
                        totalNs += ns;
                        if (ns < minNs) minNs = ns;
                        if (ns > maxNs) maxNs = ns;
                    }

                    if (totalNs > 0)
                        sceneTick = new JObject
                        {
                            ["averageFps"] = Round1(1e9f / ((float)totalNs / tickSamples)),
                            ["minFps"] = Round1(1e9f / maxNs),
                            ["maxFps"] = Round1(1e9f / minNs),
                            ["targetFps"] = metrics.TargetFps,
                        };
                }
            }

            var structured = new JObject
            {
                ["sampleSeconds"] = Round1(sampleSeconds),
                ["framesSampled"] = framesSampled,
                ["averageFps"] = Round1(averageFps),
                ["minFps"] = Round1(minFps),
                ["maxFps"] = Round1(maxFps),
                ["averageFrameMs"] = Round1(averageFrameMs),
                ["maxFrameMs"] = Round1(maxFrameMs),
                ["hiccupFrames"] = hiccupFrames,
                ["sceneTick"] = sceneTick ?? (JToken)JValue.CreateNull(),
            };

            var text = new StringBuilder();
            text.Append("Render: ").Append(averageFps.ToString("F1", CultureInfo.InvariantCulture)).Append(" fps avg (min ")
                .Append(minFps.ToString("F1", CultureInfo.InvariantCulture)).Append(", max ")
                .Append(maxFps.ToString("F1", CultureInfo.InvariantCulture)).Append(") over ")
                .Append(framesSampled).Append(" frames / ").Append(sampleSeconds.ToString("F1", CultureInfo.InvariantCulture)).Append("s; ")
                .Append(hiccupFrames).AppendLine(" hiccup frames (>50 ms).");

            if (sceneTick != null)
                text.Append("Scene tick: ").Append(sceneTick["averageFps"]!.Value<float>().ToString("F1", CultureInfo.InvariantCulture))
                    .Append(" fps avg (target ").Append(sceneTick["targetFps"]!.Value<int>()).Append(").");
            else
                text.Append("Scene tick: no data (no scene loaded or it has not ticked yet).");

            return McpToolResult.TextWithStructured(text.ToString(), structured);
        }

        private static float Round1(float value) =>
            Mathf.Round(value * 10f) / 10f;
    }
}
