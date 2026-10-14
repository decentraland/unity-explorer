using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using Cysharp.Threading.Tasks;
using DCL.ECS7;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.Profiling;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Diagnostics;
using System.Threading;
using DCL.Utilities;

namespace DCL.McpServer.Tests
{
    public class GetCrdtTrafficToolShould
    {
        private const int ENTITY = 512;
        private const int CUSTOM_COMPONENT = 5000;

        private World world = null!;
        private IScenesCache scenesCache = null!;
        private ISceneFacade scene = null!;
        private SceneRuntimeMetrics metrics = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            metrics = new SceneRuntimeMetrics();
            scene = Substitute.For<ISceneFacade>();
            scene.RuntimeMetrics.Returns(metrics);
            scene.EcsExecutor.Returns(new SceneEcsExecutor(world));
            scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(scene));
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
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
        public void DescribeWritersWithTheirEngineEntity()
        {
            // Arrange
            Entity engineEntity = world.Create(
                new CRDTEntity(ENTITY),
                new PBGltfContainer { Src = "assets/drink.glb" },
                new PBVisibilityComponent { Visible = false },
                new SDKTransform { ParentId = 600 });

            GetCrdtTrafficTool tool = CreateTool(static probe =>
            {
                probe.RecordBatch();
                probe.Record(CrdtTrafficDirection.FromScene, ENTITY, ComponentID.MESH_RENDERER, CrdtTrafficOutcome.RedundantIdenticalData, 8);
                probe.Record(CrdtTrafficDirection.FromScene, ENTITY + 1, ComponentID.TRANSFORM, CrdtTrafficOutcome.Applied, 8);
            });

            // Act
            McpToolResult result = Execute(tool, new JObject());

            // Assert
            var writers = (JArray)result.Payload["structuredContent"]!["fromScene"]!["hotWriters"]!;
            JToken described = writers[0]["entity"]!.Value<int>() == ENTITY ? writers[0] : writers[1];
            JToken missing = writers[0]["entity"]!.Value<int>() == ENTITY ? writers[1] : writers[0];

            Assert.That(described["engineEntityId"]!.Value<int>(), Is.EqualTo(engineEntity.Id));
            Assert.That(described["entityLabel"]!.Value<string>(), Is.EqualTo("gltf assets/drink.glb, hidden, parent 600"));
            Assert.That(missing["engineEntityId"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(missing["entityLabel"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain($"entity {ENTITY} [engine {engineEntity.Id}] (gltf assets/drink.glb"));
        }

        [Test]
        public void TreatAWriterAFewTicksShortAsEveryTick()
        {
            // Arrange
            GetCrdtTrafficTool tool = CreateTool(static probe =>
            {
                for (var tick = 0; tick < 100; tick++)
                {
                    probe.RecordBatch();

                    if (tick > 0)
                        probe.Record(CrdtTrafficDirection.ToScene, ENTITY, ComponentID.TRANSFORM, CrdtTrafficOutcome.Applied, 44);
                }
            });

            // Act
            McpToolResult result = Execute(tool, new JObject());

            // Assert
            var hottest = (JObject)result.Payload["structuredContent"]!["toScene"]!["hotWriters"]![0]!;
            Assert.That(hottest["verdict"]!.Value<string>(), Does.StartWith("hot: changes every tick"));
        }

        [Test]
        public void ReportBridgeTimePerSceneTick()
        {
            // Arrange
            GetCrdtTrafficTool tool = CreateTool(static probe =>
            {
                probe.RecordBatch();
                probe.RecordBatch();
                probe.AddIncomingTime(Stopwatch.Frequency / 1000);
                probe.AddApplyTime(Stopwatch.Frequency / 2000);
                probe.AddOutgoingTime(Stopwatch.Frequency / 4000);
            });

            // Act
            McpToolResult result = Execute(tool, new JObject());

            // Assert
            JToken bridgeTime = result.Payload["structuredContent"]!["bridgeTime"]!;

            Assert.That(bridgeTime["totalMs"]!.Value<double>(), Is.EqualTo(1.75).Within(0.01));
            Assert.That(bridgeTime["totalUsPerTick"]!.Value<double>(), Is.EqualTo(875).Within(1));
            Assert.That(bridgeTime["incomingUsPerTick"]!.Value<double>(), Is.EqualTo(500).Within(1));
            Assert.That(bridgeTime["applyUsPerTick"]!.Value<double>(), Is.EqualTo(250).Within(1));
            Assert.That(bridgeTime["outgoingUsPerTick"]!.Value<double>(), Is.EqualTo(125).Within(1));
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("CRDT bridge time:"));
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
