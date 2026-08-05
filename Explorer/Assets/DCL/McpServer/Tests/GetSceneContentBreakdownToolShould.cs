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
    public class GetSceneContentBreakdownToolShould
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
            var tool = new GetSceneContentBreakdownTool(scenesCache, collectionTimeoutMs: 0);

            // Act
            McpToolResult result = Execute(tool);

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
        }

        [Test]
        public void ClearBothDemandFlagsWhenNoPassArrives()
        {
            // Arrange — the zero timeout skips the wait, so no fresh pass lands and the tool errors;
            // the cleanup finally must still reset both the breakdown request and the MCP demand flag.
            var tool = new GetSceneContentBreakdownTool(scenesCache, collectionTimeoutMs: 0);

            // Act
            McpToolResult result = Execute(tool);

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(runtimeMetrics.ContentStats.BreakdownRequested, Is.False);
            Assert.That(runtimeMetrics.ContentStats.RequestedByMcp, Is.False);
        }

        [Test]
        public void DeclareLimitAndSortByInTheInputSchema()
        {
            // Arrange
            var tool = new GetSceneContentBreakdownTool(scenesCache);

            // Act
            var properties = (JObject)tool.InputSchema["properties"]!;

            // Assert
            Assert.That(properties["limit"], Is.Not.Null);
            Assert.That(properties["sortBy"], Is.Not.Null);
        }

        private static McpToolResult Execute(GetSceneContentBreakdownTool tool) =>
            tool.ExecuteAsync(new JObject(), CancellationToken.None).GetAwaiter().GetResult();
    }
}
