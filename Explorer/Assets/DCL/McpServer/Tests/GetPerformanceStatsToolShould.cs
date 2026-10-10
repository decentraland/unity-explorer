using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.Profiling;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;

namespace DCL.McpServer.Tests
{
    public class GetPerformanceStatsToolShould
    {
        private IScenesCache scenesCache = null!;
        private ISceneFacade scene = null!;
        private SceneRuntimeMetrics runtimeMetrics = null!;
        private ReactiveProperty<ISceneFacade?> currentScene = null!;

        [SetUp]
        public void Setup()
        {
            runtimeMetrics = new SceneRuntimeMetrics { TargetFps = 30 };

            scene = Substitute.For<ISceneFacade>();
            scene.RuntimeMetrics.Returns(runtimeMetrics);

            currentScene = new ReactiveProperty<ISceneFacade?>(scene);
            scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(currentScene);
        }

        [Test]
        public void ReportRenderStatsFromTheSampledWindow()
        {
            // Arrange — 100 frames totalling 2 s: 20 ms average spanning 10–50 ms, 3 above the hiccup bar
            var tool = new GetPerformanceStatsTool(scenesCache, (_, _, _) =>
                UniTask.FromResult(new GetPerformanceStatsTool.FrameWindow(100, 2000f, 10f, 50f, 3)));

            // Act
            var structured = (JObject)Execute(tool).Payload["structuredContent"]!;

            // Assert
            McpSchemaAssert.KeysMatch(tool.OutputSchema!, structured);
            Assert.That(structured["framesSampled"]!.Value<int>(), Is.EqualTo(100));
            Assert.That(structured["averageFps"]!.Value<float>(), Is.EqualTo(50f));
            Assert.That(structured["minFps"]!.Value<float>(), Is.EqualTo(20f));
            Assert.That(structured["maxFps"]!.Value<float>(), Is.EqualTo(100f));
            Assert.That(structured["averageFrameMs"]!.Value<float>(), Is.EqualTo(20f));
            Assert.That(structured["maxFrameMs"]!.Value<float>(), Is.EqualTo(50f));
            Assert.That(structured["hiccupFrames"]!.Value<int>(), Is.EqualTo(3));
        }

        [Test]
        public void ReportSceneTickStatsOnlyFromTicksInsideTheWindow()
        {
            // Arrange — stale pre-window 100 ms ticks would read as 10 fps if they leaked into the window
            for (var i = 0; i < 10; i++)
                runtimeMetrics.TickTimesNs.Add(100_000_000);

            var tool = new GetPerformanceStatsTool(scenesCache, (_, _, _) =>
            {
                for (var i = 0; i < 4; i++)
                    runtimeMetrics.TickTimesNs.Add(25_000_000); // 40 fps during the window

                return UniTask.FromResult(SteadyWindow());
            });

            // Act
            var structured = (JObject)Execute(tool).Payload["structuredContent"]!;
            var sceneTick = (JObject)structured["sceneTick"]!;

            // Assert
            McpSchemaAssert.KeysMatch(tool.OutputSchema!, structured);
            Assert.That(sceneTick["averageFps"]!.Value<float>(), Is.EqualTo(40f));
            Assert.That(sceneTick["minFps"]!.Value<float>(), Is.EqualTo(40f));
            Assert.That(sceneTick["maxFps"]!.Value<float>(), Is.EqualTo(40f));
            Assert.That(sceneTick["targetFps"]!.Value<int>(), Is.EqualTo(30));
        }

        [Test]
        public void ReportNullSceneTickWhenNoTicksLandDuringTheWindow()
        {
            // Arrange — the ring holds only stale pre-window ticks, as when the scene is paused
            for (var i = 0; i < 10; i++)
                runtimeMetrics.TickTimesNs.Add(100_000_000);

            var tool = new GetPerformanceStatsTool(scenesCache, (_, _, _) => UniTask.FromResult(SteadyWindow()));

            // Act
            var structured = (JObject)Execute(tool).Payload["structuredContent"]!;

            // Assert
            Assert.That(structured["sceneTick"]!.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public void ReportNullSceneTickWhenTheSceneChangesMidSample()
        {
            // Arrange — the current scene flips during the window, so its ticks describe a mixed window
            var tool = new GetPerformanceStatsTool(scenesCache, (_, _, _) =>
            {
                runtimeMetrics.TickTimesNs.Add(25_000_000);
                currentScene.Value = null;
                return UniTask.FromResult(SteadyWindow());
            });

            // Act
            var structured = (JObject)Execute(tool).Payload["structuredContent"]!;

            // Assert
            Assert.That(structured["sceneTick"]!.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public void ClampSampleSecondsBeforeSampling()
        {
            // Arrange
            var receivedSeconds = 0f;

            var tool = new GetPerformanceStatsTool(scenesCache, (seconds, _, _) =>
            {
                receivedSeconds = seconds;
                return UniTask.FromResult(SteadyWindow());
            });

            // Act
            var structured = (JObject)Execute(tool, new JObject { ["sampleSeconds"] = 100 }).Payload["structuredContent"]!;

            // Assert
            Assert.That(receivedSeconds, Is.EqualTo(10f));
            Assert.That(structured["sampleSeconds"]!.Value<float>(), Is.EqualTo(10f));
        }

        [Test]
        public void SampleHiccupsAgainstTheClientWideThreshold()
        {
            // Arrange
            var receivedThresholdMs = 0f;

            var tool = new GetPerformanceStatsTool(scenesCache, (_, thresholdMs, _) =>
            {
                receivedThresholdMs = thresholdMs;
                return UniTask.FromResult(SteadyWindow());
            });

            // Act
            var structured = (JObject)Execute(tool).Payload["structuredContent"]!;

            // Assert
            float expected = Profiler.EffectiveHiccupThresholdNs() / 1_000_000f;
            Assert.That(receivedThresholdMs, Is.EqualTo(expected));
            Assert.That(structured["hiccupThresholdMs"], Is.Not.Null);
        }

        [Test]
        public void ErrorWhenNoFramesWereSampled()
        {
            // Arrange
            var tool = new GetPerformanceStatsTool(scenesCache, (_, _, _) =>
                UniTask.FromResult(new GetPerformanceStatsTool.FrameWindow(0, 0f, 0f, 0f, 0)));

            // Act
            McpToolResult result = Execute(tool);

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
        }

        private static GetPerformanceStatsTool.FrameWindow SteadyWindow() =>
            new (120, 1920f, 16f, 16f, 0);

        private static McpToolResult Execute(GetPerformanceStatsTool tool, JObject? arguments = null) =>
            tool.ExecuteAsync(arguments ?? new JObject(), CancellationToken.None).GetAwaiter().GetResult();
    }
}
