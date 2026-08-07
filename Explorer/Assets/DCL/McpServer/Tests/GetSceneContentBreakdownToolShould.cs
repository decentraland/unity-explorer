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
        public void ReleaseBothDemandRefcountsWhenNoPassArrives()
        {
            // Arrange — the zero timeout skips the wait, so no fresh pass lands and the tool errors;
            // the cleanup finally must still release both the breakdown and the MCP demand refcounts.
            var tool = new GetSceneContentBreakdownTool(scenesCache, collectionTimeoutMs: 0);

            // Act
            McpToolResult result = Execute(tool);

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(runtimeMetrics.ContentStats.BreakdownRequests, Is.EqualTo(0));
            Assert.That(runtimeMetrics.ContentStats.McpRequests, Is.EqualTo(0));
        }

        [Test]
        public void ReleaseOnlyItsOwnDemandWhenCallsOverlap()
        {
            // Arrange — another in-flight tool call already holds one count of each demand
            runtimeMetrics.ContentStats.BreakdownRequests = 1;
            runtimeMetrics.ContentStats.McpRequests = 1;
            var tool = new GetSceneContentBreakdownTool(scenesCache, collectionTimeoutMs: 0);

            // Act
            Execute(tool);

            // Assert — the finished call must not cancel the other call's demand
            Assert.That(runtimeMetrics.ContentStats.BreakdownRequests, Is.EqualTo(1));
            Assert.That(runtimeMetrics.ContentStats.McpRequests, Is.EqualTo(1));
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
