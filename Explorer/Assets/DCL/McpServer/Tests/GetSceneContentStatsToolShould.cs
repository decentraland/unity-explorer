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
            Assert.That(runtimeMetrics.ContentStats.RequestedByMcp, Is.False);
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
            stats.Colliders = 2;
            stats.ExternalContent = 1;

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
            Assert.That(structured["externalContent"]!.Value<int>(), Is.EqualTo(1));
        }

        [Test]
        public void ClearTheDemandFlagAfterReporting()
        {
            // Arrange
            runtimeMetrics.ContentStats.HasData = true;
            var tool = new GetSceneContentStatsTool(scenesCache, collectionTimeoutMs: 0);

            // Act
            Execute(tool);

            // Assert
            Assert.That(runtimeMetrics.ContentStats.RequestedByMcp, Is.False);
        }

        private static McpToolResult Execute(GetSceneContentStatsTool tool) =>
            tool.ExecuteAsync(new JObject(), CancellationToken.None).GetAwaiter().GetResult();
    }
}
