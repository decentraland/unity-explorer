using Cysharp.Threading.Tasks;
using DCL.ECS7;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.Profiling;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Threading;
using DCL.Utilities;

namespace DCL.McpServer.Tests
{
    public class GetCrdtTrafficToolShould
    {
        private const int ENTITY = 512;
        private const int CUSTOM_COMPONENT = 5000;

        private IScenesCache scenesCache = null!;
        private ISceneFacade scene = null!;
        private SceneRuntimeMetrics metrics = null!;

        [SetUp]
        public void SetUp()
        {
            metrics = new SceneRuntimeMetrics();
            scene = Substitute.For<ISceneFacade>();
            scene.RuntimeMetrics.Returns(metrics);
            scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(scene));
        }

        [Test]
        public void ErrorWhenThereIsNoCurrentScene()
        {
            // Arrange
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(null));
            GetCrdtTrafficTool tool = CreateTool(null);

            // Act
            McpToolResult result = Execute(tool, new JObject());

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
        }

        [Test]
        public void ErrorWhenTheSceneDidNotTick()
        {
            // Arrange
            GetCrdtTrafficTool tool = CreateTool(null);

            // Act
            McpToolResult result = Execute(tool, new JObject());

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(metrics.Traffic.IsCapturing, Is.False);
        }

        [Test]
        public void RefuseAConcurrentCapture()
        {
            // Arrange
            metrics.Traffic.TryStartCapture();
            GetCrdtTrafficTool tool = CreateTool(null);

            // Act
            McpToolResult result = Execute(tool, new JObject());

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("already running"));
            Assert.That(metrics.Traffic.IsCapturing, Is.True);
        }

        [Test]
        public void MatchTheOutputSchemaToThePayload()
        {
            // Arrange
            GetCrdtTrafficTool tool = CreateTool(static probe =>
            {
                probe.RecordBatch();
                probe.Record(CrdtTrafficDirection.FromScene, ENTITY, ComponentID.TRANSFORM, CrdtTrafficOutcome.Applied, 10);
                probe.Record(CrdtTrafficDirection.ToScene, 1, ComponentID.ENGINE_INFO, CrdtTrafficOutcome.RedundantIdenticalData, 4);
            });

            // Act
            McpToolResult result = Execute(tool, new JObject());

            // Assert
            McpSchemaAssert.KeysMatch(tool.OutputSchema, (JObject)result.Payload["structuredContent"]!);
        }

        [Test]
        public void FlagIdenticalRewritesEveryTickAsWasted()
        {
            // Arrange
            GetCrdtTrafficTool tool = CreateTool(static probe =>
            {
                for (var tick = 0; tick < 10; tick++)
                {
                    probe.RecordBatch();
                    probe.Record(CrdtTrafficDirection.FromScene, ENTITY, ComponentID.TRANSFORM, CrdtTrafficOutcome.RedundantIdenticalData, 32);
                }

                probe.Record(CrdtTrafficDirection.FromScene, ENTITY, ComponentID.MATERIAL, CrdtTrafficOutcome.Applied, 64);
            });

            // Act
            McpToolResult result = Execute(tool, new JObject());

            // Assert
            var fromScene = (JObject)result.Payload["structuredContent"]!["fromScene"]!;
            var hottest = (JObject)fromScene["hotWriters"]![0]!;

            Assert.That(result.Payload["structuredContent"]!["sceneTicks"]!.Value<int>(), Is.EqualTo(10));
            Assert.That(fromScene["messages"]!.Value<int>(), Is.EqualTo(11));
            Assert.That(fromScene["wasted"]!.Value<int>(), Is.EqualTo(10));
            Assert.That(fromScene["redundantIdenticalData"]!.Value<int>(), Is.EqualTo(10));
            Assert.That(fromScene["distinctWriters"]!.Value<int>(), Is.EqualTo(2));
            Assert.That(hottest["component"]!.Value<string>(), Is.EqualTo("Transform"));
            Assert.That(hottest["entity"]!.Value<int>(), Is.EqualTo(ENTITY));
            Assert.That(hottest["perTick"]!.Value<float>(), Is.EqualTo(1f));
            Assert.That(hottest["verdict"]!.Value<string>(), Does.StartWith("wasted: rewrites identical values every tick"));
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("entity 512 Transform: 10 msgs (1.0/tick)"));
        }

        [Test]
        public void NameReservedEntitiesAndCustomComponents()
        {
            // Arrange
            GetCrdtTrafficTool tool = CreateTool(static probe =>
            {
                probe.RecordBatch();
                probe.Record(CrdtTrafficDirection.ToScene, 1, ComponentID.ENGINE_INFO, CrdtTrafficOutcome.Applied, 4);
                probe.Record(CrdtTrafficDirection.FromScene, ENTITY, CUSTOM_COMPONENT, CrdtTrafficOutcome.FilteredCreatorHub, 0);
            });

            // Act
            McpToolResult result = Execute(tool, new JObject());

            // Assert
            JToken structured = result.Payload["structuredContent"]!;
            var toScene = (JObject)structured["toScene"]!["hotWriters"]![0]!;
            var fromScene = (JObject)structured["fromScene"]!["hotWriters"]![0]!;

            Assert.That(toScene["entityLabel"]!.Value<string>(), Is.EqualTo("player"));
            Assert.That(toScene["component"]!.Value<string>(), Is.EqualTo("EngineInfo"));
            Assert.That(toScene["verdict"]!.Value<string>(), Does.StartWith("hot"));
            Assert.That(fromScene["entityLabel"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(fromScene["component"]!.Value<string>(), Is.EqualTo("custom:5000"));
            Assert.That(fromScene["verdict"]!.Value<string>(), Does.StartWith("wasted: Creator Hub"));
        }

        [Test]
        public void HonourTheLimitAndSortByMessages()
        {
            // Arrange
            GetCrdtTrafficTool tool = CreateTool(static probe =>
            {
                probe.RecordBatch();

                for (var entity = 1; entity <= 3; entity++)
                for (var i = 0; i < entity; i++)
                    probe.Record(CrdtTrafficDirection.FromScene, ENTITY + entity, ComponentID.TRANSFORM, CrdtTrafficOutcome.Applied, 1);
            });

            // Act
            McpToolResult result = Execute(tool, new JObject { ["limit"] = 2 });

            // Assert
            var writers = (JArray)result.Payload["structuredContent"]!["fromScene"]!["hotWriters"]!;

            Assert.That(writers.Count, Is.EqualTo(2));
            Assert.That(writers[0]["entity"]!.Value<int>(), Is.EqualTo(ENTITY + 3));
            Assert.That(writers[1]["entity"]!.Value<int>(), Is.EqualTo(ENTITY + 2));
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("1 more writers"));
        }

        private static McpToolResult Execute(GetCrdtTrafficTool tool, JObject arguments) =>
            tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();

        private GetCrdtTrafficTool CreateTool(Action<CrdtTrafficProbe>? feed) =>
            new (scenesCache, (_, _) =>
            {
                feed?.Invoke(metrics.Traffic);
                return UniTask.CompletedTask;
            });
    }
}
