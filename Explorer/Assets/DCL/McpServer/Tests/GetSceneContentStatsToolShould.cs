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
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tests
{
    public class GetSceneContentStatsToolShould
    {
        private IScenesCache scenesCache = null!;
        private ISceneFacade scene = null!;
        private SceneRuntimeMetrics runtimeMetrics = null!;

        [SetUp]
        public void Setup()
        {
            runtimeMetrics = new SceneRuntimeMetrics();

            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.Parcels.Returns(new List<Vector2Int> { new (0, 0), new (0, 1) });

            scene = Substitute.For<ISceneFacade>();
            scene.SceneData.Returns(sceneData);
            scene.RuntimeMetrics.Returns(runtimeMetrics);

            scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(scene));
        }

        [Test]
        public void ErrorWhenNoSceneIsLoaded()
        {
            // Arrange
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(null));
            var tool = new GetSceneContentStatsTool(scenesCache, collectionTimeoutMs: 0);

            // Act
            McpToolResult result = Execute(tool);

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
        }

        [Test]
        public void ErrorWhenTheSceneNeverProducedStats()
        {
            // Arrange — HasData stays false and the zero timeout skips waiting for a pass
            var tool = new GetSceneContentStatsTool(scenesCache, collectionTimeoutMs: 0);

            // Act
            McpToolResult result = Execute(tool);

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(runtimeMetrics.ContentStats.McpRequests, Is.EqualTo(0));
        }

        [Test]
        public void ReportStatsWithCapsMatchingTheOutputSchema()
        {
            // Arrange
            SceneContentStats stats = runtimeMetrics.ContentStats;
            stats.HasData = true;
            stats.Entities = 10;
            stats.Triangles = 5200;
            stats.Bodies = 12;
            stats.Geometries = 7;
            stats.Materials = 5;
            stats.Textures = 3;
            stats.ShaderVariants = 2;
            stats.Colliders = 2;
            stats.Videos = 1;

            var tool = new GetSceneContentStatsTool(scenesCache, collectionTimeoutMs: 0);

            // Act
            var structured = (JObject)Execute(tool).Payload["structuredContent"]!;

            // Assert
            McpSchemaAssert.KeysMatch(tool.OutputSchema, structured);
            Assert.That(structured["parcelCount"]!.Value<int>(), Is.EqualTo(2));
            Assert.That(structured["fresh"]!.Value<bool>(), Is.False);
            Assert.That(structured["entities"]!.Value<int>(), Is.EqualTo(10));
            Assert.That(structured["entitiesCap"]!.Value<int>(), Is.EqualTo(400));
            Assert.That(structured["triangles"]!.Value<long>(), Is.EqualTo(5200));
            Assert.That(structured["trianglesCap"]!.Value<long>(), Is.EqualTo(20000));
            Assert.That(structured["geometries"]!.Value<int>(), Is.EqualTo(7));
            Assert.That(structured["shaderVariants"]!.Value<int>(), Is.EqualTo(2));
            Assert.That(structured["videos"]!.Value<int>(), Is.EqualTo(1));
        }

        [Test]
        public void ReleaseTheDemandRefcountAfterReporting()
        {
            // Arrange
            runtimeMetrics.ContentStats.HasData = true;
            var tool = new GetSceneContentStatsTool(scenesCache, collectionTimeoutMs: 0);

            // Act
            Execute(tool);

            // Assert
            Assert.That(runtimeMetrics.ContentStats.McpRequests, Is.EqualTo(0));
        }

        [Test]
        public void ReportFreshStatsWhenAPassLandsDuringTheWait()
        {
            // Arrange — the injected wait stands in for the scene world completing a counting pass
            var tool = new GetSceneContentStatsTool(scenesCache, waitForCollection: (_, _, s, before, _, _) =>
            {
                s.HasData = true;
                s.CollectionCount = before + 1;
                s.Entities = 10;
                return UniTask.FromResult(true);
            });

            // Act
            var structured = (JObject)Execute(tool).Payload["structuredContent"]!;

            // Assert
            Assert.That(structured["fresh"]!.Value<bool>(), Is.True);
            Assert.That(structured["entities"]!.Value<int>(), Is.EqualTo(10));
        }

        [Test]
        public void ErrorWhenTheCurrentSceneChangesMidWait()
        {
            // Arrange
            var tool = new GetSceneContentStatsTool(scenesCache, waitForCollection: (_, _, _, _, _, _) => UniTask.FromResult(false));

            // Act
            McpToolResult result = Execute(tool);

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
        }

        [Test]
        public void ReleaseOnlyItsOwnDemandWhenCallsOverlap()
        {
            // Arrange — another in-flight tool call already holds one demand count
            runtimeMetrics.ContentStats.HasData = true;
            runtimeMetrics.ContentStats.McpRequests = 1;
            var tool = new GetSceneContentStatsTool(scenesCache, collectionTimeoutMs: 0);

            // Act
            Execute(tool);

            // Assert — the finished call must not cancel the other call's demand
            Assert.That(runtimeMetrics.ContentStats.McpRequests, Is.EqualTo(1));
        }

        private static McpToolResult Execute(GetSceneContentStatsTool tool) =>
            tool.ExecuteAsync(new JObject(), CancellationToken.None).GetAwaiter().GetResult();
    }
}
