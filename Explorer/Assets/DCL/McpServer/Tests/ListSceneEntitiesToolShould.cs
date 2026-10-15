using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;

namespace DCL.McpServer.Tests
{
    public class ListSceneEntitiesToolShould
    {
        private World world = null!;
        private IScenesCache scenesCache = null!;
        private ListSceneEntitiesTool tool = null!;

        [SetUp]
        public void Setup()
        {
            world = World.Create();
            ISceneFacade scene = Substitute.For<ISceneFacade>();
            scene.EcsExecutor.Returns(new SceneEcsExecutor(world));
            scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(scene));
            tool = new ListSceneEntitiesTool(scenesCache);
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

            // Act
            McpToolResult result = Execute(new JObject());

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
        }

        [Test]
        public void AddAnActionableLineWhenNotEverythingIsShown()
        {
            // Arrange
            CreateEntities(10);

            // Act
            string text = TextOf(Execute(new JObject { ["limit"] = 3 }));

            // Assert
            Assert.That(text, Does.Contain("total=10 returned=3"));
            Assert.That(text, Does.Contain("3 of 10 shown"));
        }

        [Test]
        public void OmitTheActionableLineWhenEverythingFits()
        {
            // Arrange
            CreateEntities(3);

            // Act
            string text = TextOf(Execute(new JObject { ["limit"] = 200 }));

            // Assert
            Assert.That(text, Does.Contain("total=3 returned=3"));
            Assert.That(text, Does.Not.Contain("shown"));
        }

        [Test]
        public void MirrorTheListingInStructuredContentWhileKeepingTheText()
        {
            // Arrange
            List<Entity> created = CreateEntities(10);

            // Act
            McpToolResult result = Execute(new JObject { ["limit"] = 3 });

            // Assert
            Assert.That(TextOf(result), Does.Contain("total=10 returned=3"));

            var structured = (JObject)result.Payload["structuredContent"]!;
            Assert.That(structured["total"]!.Value<int>(), Is.EqualTo(10));
            Assert.That(structured["matched"]!.Value<int>(), Is.EqualTo(10));
            Assert.That(structured["returned"]!.Value<int>(), Is.EqualTo(3));
            Assert.That(structured["truncated"]!.Value<bool>(), Is.True);
            Assert.That(((JArray)structured["entityIds"]!).ToObject<int[]>(), Is.EqualTo(new[] { created[0].Id, created[1].Id, created[2].Id }));
        }

        [Test]
        public void DescribeEachEntityWithItsCrdtIdAndLabel()
        {
            // Arrange
            Entity root = world.Create(new CRDTEntity(0), new SceneRootComponent());
            Entity drink = world.Create(new CRDTEntity(1736), new PBGltfContainer { Src = "assets/drink.glb" }, new SDKTransform { ParentId = 1735 });
            Entity engineOnly = world.Create();

            // Act
            McpToolResult result = Execute(new JObject());

            // Assert
            var entities = (JArray)result.Payload["structuredContent"]!["entities"]!;
            JToken rootEntry = Find(entities, root.Id);
            JToken drinkEntry = Find(entities, drink.Id);
            JToken engineOnlyEntry = Find(entities, engineOnly.Id);

            Assert.That(rootEntry["crdtEntity"]!.Value<int>(), Is.EqualTo(0));
            Assert.That(rootEntry["label"]!.Value<string>(), Is.EqualTo("scene root"));
            Assert.That(drinkEntry["crdtEntity"]!.Value<int>(), Is.EqualTo(1736));
            Assert.That(drinkEntry["label"]!.Value<string>(), Is.EqualTo("gltf assets/drink.glb, parent 1735"));
            Assert.That(engineOnlyEntry["crdtEntity"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(engineOnlyEntry["label"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(TextOf(result), Does.Contain($"{drink.Id} crdt=1736 (gltf assets/drink.glb, parent 1735)"));
        }

        [Test]
        public void FindTheEngineEntityOfACrdtId()
        {
            // Arrange
            world.Create(new CRDTEntity(512));
            Entity target = world.Create(new CRDTEntity(1874), new PBMeshRenderer { Plane = new PBMeshRenderer.Types.PlaneMesh() });

            // Act
            McpToolResult result = Execute(new JObject { ["crdtEntity"] = 1874 });

            // Assert
            var structured = (JObject)result.Payload["structuredContent"]!;
            Assert.That(structured["total"]!.Value<int>(), Is.EqualTo(2));
            Assert.That(structured["matched"]!.Value<int>(), Is.EqualTo(1));
            Assert.That(structured["entities"]![0]!["id"]!.Value<int>(), Is.EqualTo(target.Id));
            Assert.That(structured["entities"]![0]!["label"]!.Value<string>(), Is.EqualTo("mesh Plane"));
            Assert.That(TextOf(result), Does.Contain("total=2 matched=1 returned=1"));
        }

        [Test]
        public void ListTheSceneContainerWithoutTheRootCrdtId()
        {
            // Arrange
            Entity root = world.Create(new CRDTEntity(0), new SceneRootComponent());
            Entity container = world.Create(new CRDTEntity());

            // Act
            McpToolResult listing = Execute(new JObject());
            McpToolResult byRootId = Execute(new JObject { ["crdtEntity"] = 0 });

            // Assert
            JToken containerEntry = Find((JArray)listing.Payload["structuredContent"]!["entities"]!, container.Id);
            var rootMatches = (JArray)byRootId.Payload["structuredContent"]!["entities"]!;

            Assert.That(containerEntry["crdtEntity"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(containerEntry["label"]!.Value<string>(), Is.EqualTo("scene container"));
            Assert.That(rootMatches.Count, Is.EqualTo(1));
            Assert.That(rootMatches[0]["id"]!.Value<int>(), Is.EqualTo(root.Id));
        }

        [Test]
        public void FilterOnTheLabelIgnoringCase()
        {
            // Arrange
            Entity toy = world.Create(new CRDTEntity(1276), new PBGltfContainer { Src = "assets/environment/PoolToy02.glb" });
            world.Create(new CRDTEntity(1277), new PBGltfContainer { Src = "assets/environment/Chimney_01.glb" });
            world.Create();

            // Act
            McpToolResult result = Execute(new JObject { ["filter"] = "pooltoy" });

            // Assert
            var entities = (JArray)result.Payload["structuredContent"]!["entities"]!;
            Assert.That(entities.Count, Is.EqualTo(1));
            Assert.That(entities[0]["id"]!.Value<int>(), Is.EqualTo(toy.Id));
        }

        [Test]
        public void KeepTheOutputSchemaInSyncWithTheStructuredPayload()
        {
            // Arrange
            CreateEntities(3);

            // Act
            var structured = (JObject)Execute(new JObject()).Payload["structuredContent"]!;

            // Assert
            McpSchemaAssert.KeysMatch(tool.OutputSchema, structured);
        }

        private McpToolResult Execute(JObject arguments) =>
            tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();

        private List<Entity> CreateEntities(int count)
        {
            var entities = new List<Entity>(count);

            for (var i = 0; i < count; i++)
                entities.Add(world.Create(new CRDTEntity(600 + i)));

            return entities;
        }

        private static JToken Find(JArray entities, int id)
        {
            foreach (JToken entry in entities)
            {
                if (entry["id"]!.Value<int>() == id)
                    return entry;
            }

            throw new AssertionException($"No entity {id} in the listing");
        }

        private static string TextOf(McpToolResult result) =>
            result.Payload["content"]![0]!["text"]!.Value<string>()!;
    }
}
