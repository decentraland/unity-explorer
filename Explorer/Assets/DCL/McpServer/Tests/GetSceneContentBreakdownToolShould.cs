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
        public void RankEntriesByTrianglesWithShareMathAndSceneTotals()
        {
            // Arrange
            GetSceneContentBreakdownTool tool = ToolWithLandedPass();

            // Act
            var structured = (JObject)Execute(tool).Payload["structuredContent"]!;
            var entries = (JArray)structured["entries"]!;

            // Assert — default sort is triangles, heaviest first
            Assert.That(entries.Count, Is.EqualTo(3));
            Assert.That(entries[0]!["source"]!.Value<string>(), Is.EqualTo("heavy.glb"));
            Assert.That(entries[1]!["source"]!.Value<string>(), Is.EqualTo("mid.glb"));
            Assert.That(entries[2]!["source"]!.Value<string>(), Is.EqualTo("light.glb"));

            // 600 of the scene's 1000 triangles; 100 of the 400 visible ones
            Assert.That(entries[0]!["trianglesSharePercent"]!.Value<float>(), Is.EqualTo(60f));
            Assert.That(entries[0]!["visibleTrianglesSharePercent"]!.Value<float>(), Is.EqualTo(25f));

            // Scene totals sum across ALL sources, not only the returned ones
            Assert.That(structured["sceneDrawCallsEstimate"]!.Value<int>(), Is.EqualTo(10));
            Assert.That(structured["visibleTriangles"]!.Value<long>(), Is.EqualTo(400));
            Assert.That(structured["visibleDrawCallsEstimate"]!.Value<int>(), Is.EqualTo(5));
            Assert.That(structured["totalSources"]!.Value<int>(), Is.EqualTo(3));
        }

        [Test]
        public void SortByVisibleTrianglesWhenRequested()
        {
            // Arrange — mid.glb is lighter overall but the heaviest from this point of view
            GetSceneContentBreakdownTool tool = ToolWithLandedPass();

            // Act
            var structured = (JObject)Execute(tool, new JObject { ["sortBy"] = "visibleTriangles" }).Payload["structuredContent"]!;
            var entries = (JArray)structured["entries"]!;

            // Assert
            Assert.That(structured["sortedBy"]!.Value<string>(), Is.EqualTo("visibleTriangles"));
            Assert.That(entries[0]!["source"]!.Value<string>(), Is.EqualTo("mid.glb"));
            Assert.That(entries[1]!["source"]!.Value<string>(), Is.EqualTo("heavy.glb"));
            Assert.That(entries[2]!["source"]!.Value<string>(), Is.EqualTo("light.glb"));
        }

        [Test]
        public void ClampTheLimitAndReportTheTruncation()
        {
            // Arrange
            GetSceneContentBreakdownTool tool = ToolWithLandedPass();

            // Act — 0 clamps to the minimum of 1 entry
            var structured = (JObject)Execute(tool, new JObject { ["limit"] = 0 }).Payload["structuredContent"]!;

            // Assert
            Assert.That(structured["returned"]!.Value<int>(), Is.EqualTo(1));
            Assert.That(((JArray)structured["entries"]!).Count, Is.EqualTo(1));
            Assert.That(structured["totalSources"]!.Value<int>(), Is.EqualTo(3));
        }

        [Test]
        public void ErrorWhenTheCurrentSceneChangesMidWait()
        {
            // Arrange
            var tool = new GetSceneContentBreakdownTool(scenesCache, waitForCollection: (_, _, _, _, _, _) => UniTask.FromResult(false));

            // Act
            McpToolResult result = Execute(tool);

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
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

        /// <summary>
        ///     A tool whose injected wait behaves like a counting pass landing: the entries are already
        ///     in place (as the scene world would leave them) and the collection count advances.
        /// </summary>
        private GetSceneContentBreakdownTool ToolWithLandedPass()
        {
            SceneContentStats stats = runtimeMetrics.ContentStats;
            stats.Triangles = 1000;
            stats.Materials = 6;
            stats.ShaderVariants = 2;
            stats.BreakdownEntries.Add(Entry("heavy.glb", triangles: 600, drawCalls: 5, visibleTriangles: 100, visibleDrawCalls: 2));
            stats.BreakdownEntries.Add(Entry("mid.glb", triangles: 300, drawCalls: 4, visibleTriangles: 300, visibleDrawCalls: 3));
            stats.BreakdownEntries.Add(Entry("light.glb", triangles: 100, drawCalls: 1, visibleTriangles: 0, visibleDrawCalls: 0));

            return new GetSceneContentBreakdownTool(scenesCache, waitForCollection: (_, _, s, before, _, _) =>
            {
                s.CollectionCount = before + 1;
                return UniTask.FromResult(true);
            });
        }

        private static SceneContentBreakdownEntry Entry(string source, long triangles, int drawCalls, long visibleTriangles, int visibleDrawCalls) =>
            new ()
            {
                Source = source,
                Instances = 1,
                Renderers = 2,
                Triangles = triangles,
                Materials = 2,
                DrawCalls = drawCalls,
                ShaderVariants = 1,
                VisibleRenderers = 1,
                VisibleTriangles = visibleTriangles,
                VisibleDrawCalls = visibleDrawCalls,
            };

        private static McpToolResult Execute(GetSceneContentBreakdownTool tool, JObject? arguments = null) =>
            tool.ExecuteAsync(arguments ?? new JObject(), CancellationToken.None).GetAwaiter().GetResult();
    }
}
