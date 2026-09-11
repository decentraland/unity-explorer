using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.Profiling;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Samples the client's real frame rate over its own short window (no shared profiler state
    ///     is touched) and reports it together with the current scene's tick FPS measured over the
    ///     same window, so an agent can correlate a viewpoint's content cost with the frame rate it
    ///     actually produces.
    /// </summary>
    public class GetPerformanceStatsTool : McpTool
    {
        private const float DEFAULT_SAMPLE_SECONDS = 2f;
        private const float MIN_SAMPLE_SECONDS = 0.5f;
        private const float MAX_SAMPLE_SECONDS = 10f;

        private readonly IScenesCache scenesCache;
        private readonly SampleFrames sampleFrames;
        private readonly long[] tickScratch = new long[SampledCounter.BUFFER_CAPACITY];

        public override string Name => "get_performance_stats";

        public override string Description =>
            "Sample the client's real frame rate over a short window and report render FPS (average, min, max, hiccup frames — frames above "
            + "the client's hiccup threshold, the max of 50 ms and 2x the target frame time) plus "
            + "the current scene's tick FPS vs its target, both measured over the same window. The call holds for sampleSeconds while it measures. Use together with "
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
                          .Integer("hiccupFrames", "Frames longer than hiccupThresholdMs in the window.")
                          .Number("hiccupThresholdMs", "Hiccup threshold used: the client-wide definition, max of 50 ms and 2x the target frame time.")
                          .Object("sceneTick", McpJsonSchema.Object()
                                                             .Number("averageFps")
                                                             .Number("minFps")
                                                             .Number("maxFps")
                                                             .Integer("targetFps"),
                              "The current scene's JS tick rate over the sampling window, or null when no scene is loaded, the scene changed mid-window, or it did not tick during the window (e.g. paused).", nullable: true)
                          .Build();

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("sampleSeconds", "Seconds to sample the frame rate, clamped to 0.5–10. Default 2. The call holds for this duration while it measures.");

        public GetPerformanceStatsTool(IScenesCache scenesCache, SampleFrames? sampleFrames = null)
        {
            this.scenesCache = scenesCache;
            this.sampleFrames = sampleFrames ?? SampleRealFramesAsync;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            float sampleSeconds = Mathf.Clamp(arguments.GetFloat("sampleSeconds", DEFAULT_SAMPLE_SECONDS), MIN_SAMPLE_SECONDS, MAX_SAMPLE_SECONDS);
            float hiccupThresholdMs = Profiler.EffectiveHiccupThresholdNs() / 1_000_000f;

            ISceneFacade? sceneAtStart = scenesCache.CurrentScene.Value;
            long ticksAddedBefore = sceneAtStart?.RuntimeMetrics.TickTimesNs.AddedCount ?? 0;

            FrameWindow window = await sampleFrames(sampleSeconds, hiccupThresholdMs, ct);

            if (window.FramesSampled == 0)
                return McpToolResult.Error("No frames rendered during the sampling window.");

            float averageFrameMs = window.TotalMs / window.FramesSampled;
            float averageFps = 1000f / averageFrameMs;
            float minFps = 1000f / window.MaxFrameMs;
            float maxFps = 1000f / window.MinFrameMs;

            JObject? sceneTick = null;
            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (scene != null && ReferenceEquals(scene, sceneAtStart))
            {
                SceneRuntimeMetrics metrics = scene.RuntimeMetrics;
                long ticksInWindow = metrics.TickTimesNs.AddedCount - ticksAddedBefore;
                int tickSamples = metrics.TickTimesNs.CopySnapshot(tickScratch);
                var usedSamples = (int)Math.Min(ticksInWindow, tickSamples);

                if (usedSamples > 0)
                {
                    long totalNs = 0;
                    long minNs = long.MaxValue;
                    long maxNs = long.MinValue;
                    var validSamples = 0;

                    for (int i = tickSamples - usedSamples; i < tickSamples; i++)
                    {
                        long ns = tickScratch[i];
                        if (ns <= 0) continue;
                        totalNs += ns;
                        validSamples++;
                        if (ns < minNs) minNs = ns;
                        if (ns > maxNs) maxNs = ns;
                    }

                    if (validSamples > 0)
                        sceneTick = new JObject
                        {
                            ["averageFps"] = Round1(1e9f / ((float)totalNs / validSamples)),
                            ["minFps"] = Round1(1e9f / maxNs),
                            ["maxFps"] = Round1(1e9f / minNs),
                            ["targetFps"] = metrics.TargetFps,
                        };
                }
            }

            var structured = new JObject
            {
                ["sampleSeconds"] = Round1(sampleSeconds),
                ["framesSampled"] = window.FramesSampled,
                ["averageFps"] = Round1(averageFps),
                ["minFps"] = Round1(minFps),
                ["maxFps"] = Round1(maxFps),
                ["averageFrameMs"] = Round1(averageFrameMs),
                ["maxFrameMs"] = Round1(window.MaxFrameMs),
                ["hiccupFrames"] = window.HiccupFrames,
                ["hiccupThresholdMs"] = Round1(hiccupThresholdMs),
                ["sceneTick"] = sceneTick ?? (JToken)JValue.CreateNull(),
            };

            var text = new StringBuilder();
            text.Append("Render: ").Append(averageFps.ToString("F1", CultureInfo.InvariantCulture)).Append(" fps avg (min ")
                .Append(minFps.ToString("F1", CultureInfo.InvariantCulture)).Append(", max ")
                .Append(maxFps.ToString("F1", CultureInfo.InvariantCulture)).Append(") over ")
                .Append(window.FramesSampled).Append(" frames / ").Append(sampleSeconds.ToString("F1", CultureInfo.InvariantCulture)).Append("s; ")
                .Append(window.HiccupFrames).Append(" hiccup frames (>").Append(hiccupThresholdMs.ToString("F0", CultureInfo.InvariantCulture)).AppendLine(" ms).");

            if (sceneTick != null)
                text.Append("Scene tick: ").Append(sceneTick["averageFps"]!.Value<float>().ToString("F1", CultureInfo.InvariantCulture))
                    .Append(" fps avg (target ").Append(sceneTick["targetFps"]!.Value<int>()).Append(").");
            else
                text.Append("Scene tick: no data (no scene loaded, scene changed mid-sample, or no ticks during the window).");

            return McpToolResult.TextWithStructured(text.ToString(), structured);
        }

        private static async UniTask<FrameWindow> SampleRealFramesAsync(float sampleSeconds, float hiccupThresholdMs, CancellationToken ct)
        {
            var framesSampled = 0;
            float totalMs = 0f;
            float minFrameMs = float.MaxValue;
            float maxFrameMs = 0f;
            var hiccupFrames = 0;

            float start = UnityEngine.Time.realtimeSinceStartup;

            while (UnityEngine.Time.realtimeSinceStartup - start < sampleSeconds)
            {
                await UniTask.NextFrame(ct);

                float frameMs = UnityEngine.Time.unscaledDeltaTime * 1000f;
                framesSampled++;
                totalMs += frameMs;
                if (frameMs < minFrameMs) minFrameMs = frameMs;
                if (frameMs > maxFrameMs) maxFrameMs = frameMs;
                if (frameMs > hiccupThresholdMs) hiccupFrames++;
            }

            return new FrameWindow(framesSampled, totalMs, minFrameMs, maxFrameMs, hiccupFrames);
        }

        private static float Round1(float value) =>
            Mathf.Round(value * 10f) / 10f;

        /// <summary>
        ///     Shape of <see cref="SampleRealFramesAsync" />, the loop that holds for the sampling window
        ///     and measures every rendered frame. Injectable so tests can supply a canned window.
        /// </summary>
        public delegate UniTask<FrameWindow> SampleFrames(float sampleSeconds, float hiccupThresholdMs, CancellationToken ct);

        /// <summary>
        ///     Aggregate of one frame-sampling window. <see cref="MinFrameMs" /> and <see cref="MaxFrameMs" />
        ///     are only meaningful when <see cref="FramesSampled" /> is above zero.
        /// </summary>
        public readonly struct FrameWindow
        {
            public readonly int FramesSampled;
            public readonly float TotalMs;
            public readonly float MinFrameMs;
            public readonly float MaxFrameMs;
            public readonly int HiccupFrames;

            public FrameWindow(int framesSampled, float totalMs, float minFrameMs, float maxFrameMs, int hiccupFrames)
            {
                FramesSampled = framesSampled;
                TotalMs = totalMs;
                MinFrameMs = minFrameMs;
                MaxFrameMs = maxFrameMs;
                HiccupFrames = hiccupFrames;
            }
        }
    }
}
