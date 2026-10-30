using Arch.Core;
using CRDT;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using ECS.Unity.PrimitiveRenderer.Components;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tests
{
    public class GetVisibleEntitiesToolShould
    {
        private World globalWorld = null!;
        private World sceneWorld = null!;
        private IScenesCache scenesCache = null!;
        private GameObject cameraGo = null!;
        private GameObject playerGo = null!;
        private readonly List<GameObject> cubes = new ();
        private GetVisibleEntitiesTool tool = null!;

        [SetUp]
        public void SetUp()
        {
            globalWorld = World.Create();
            sceneWorld = World.Create();

            // A camera at the origin looking down +Z, and the player at its feet.
            cameraGo = new GameObject(nameof(GetVisibleEntitiesToolShould) + "-camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            globalWorld.Create(new CameraComponent(camera));

            playerGo = new GameObject(nameof(GetVisibleEntitiesToolShould) + "-player");
            Entity playerEntity = globalWorld.Create(new CharacterTransform(playerGo.transform));

            ISceneFacade scene = Substitute.For<ISceneFacade>();
            scene.EcsExecutor.Returns(new SceneEcsExecutor(sceneWorld));
            scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(scene));

            tool = new GetVisibleEntitiesTool(scenesCache, globalWorld, playerEntity);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject cube in cubes)
                Object.DestroyImmediate(cube);

            cubes.Clear();
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(cameraGo);
            sceneWorld.Dispose();
            globalWorld.Dispose();
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
        public void ExcludeEntitiesOutsideTheFrustum()
        {
            // Arrange
            Entity inFront = AddCube(600, new Vector3(0f, 0f, 3f));
            AddCube(601, new Vector3(0f, 0f, -5f));

            // Act
            JObject structured = StructuredOf(Execute(new JObject()));

            // Assert
            Assert.That(structured["candidates"]!.Value<int>(), Is.EqualTo(2));
            Assert.That(structured["inFrustum"]!.Value<int>(), Is.EqualTo(1));
            var entities = (JArray)structured["entities"]!;
            Assert.That(entities.Count, Is.EqualTo(1));
            Assert.That(entities[0]["id"]!.Value<int>(), Is.EqualTo(inFront.Id));
            Assert.That(entities[0]["crdtEntity"]!.Value<int>(), Is.EqualTo(600));
        }

        [Test]
        public void SortByScreenCoverageDescending()
        {
            // Arrange
            Entity far = AddCube(600, new Vector3(0f, 0f, 10f));
            Entity near = AddCube(601, new Vector3(0f, 0f, 3f));

            // Act
            var entities = (JArray)StructuredOf(Execute(new JObject()))["entities"]!;

            // Assert
            Assert.That(entities[0]["id"]!.Value<int>(), Is.EqualTo(near.Id));
            Assert.That(entities[1]["id"]!.Value<int>(), Is.EqualTo(far.Id));
            Assert.That(entities[0]["coverage"]!.Value<float>(), Is.GreaterThan(entities[1]["coverage"]!.Value<float>()));
            Assert.That(entities[0]["distance"]!.Value<float>(), Is.LessThan(entities[1]["distance"]!.Value<float>()));
        }

        [Test]
        public void TruncateAtTheLimit()
        {
            // Arrange
            AddCube(600, new Vector3(0f, 0f, 3f));
            AddCube(601, new Vector3(0f, 0f, 6f));
            AddCube(602, new Vector3(0f, 0f, 9f));

            // Act
            McpToolResult result = Execute(new JObject { ["limit"] = 2 });

            // Assert
            JObject structured = StructuredOf(result);
            Assert.That(structured["returned"]!.Value<int>(), Is.EqualTo(2));
            Assert.That(structured["truncated"]!.Value<bool>(), Is.True);
            Assert.That(((JArray)structured["entities"]!).Count, Is.EqualTo(2));
            Assert.That(TextOf(result), Does.Contain("2 of 3 shown"));
        }

        [Test]
        public void DropEntriesBelowMinCoverage()
        {
            // Arrange
            Entity near = AddCube(600, new Vector3(0f, 0f, 3f));
            AddCube(601, new Vector3(0f, 0f, 40f));

            // Act
            var entities = (JArray)StructuredOf(Execute(new JObject { ["minCoverage"] = 0.01f }))["entities"]!;

            // Assert
            Assert.That(entities.Count, Is.EqualTo(1));
            Assert.That(entities[0]["id"]!.Value<int>(), Is.EqualTo(near.Id));
        }

        [Test]
        public void ReportRectsInsideTheScreenWithANormalizedCenter()
        {
            // Arrange
            AddCube(600, new Vector3(0f, 0f, 3f));

            // Act
            JObject structured = StructuredOf(Execute(new JObject()));

            // Assert
            JToken entry = structured["entities"]![0]!;
            JToken rect = entry["screenRect"]!;
            int width = structured["screen"]!["width"]!.Value<int>();
            int height = structured["screen"]!["height"]!.Value<int>();

            Assert.That(rect["x"]!.Value<float>(), Is.GreaterThanOrEqualTo(0f));
            Assert.That(rect["y"]!.Value<float>(), Is.GreaterThanOrEqualTo(0f));
            Assert.That(rect["x"]!.Value<float>() + rect["width"]!.Value<float>(), Is.LessThanOrEqualTo(width + 0.5f));
            Assert.That(rect["y"]!.Value<float>() + rect["height"]!.Value<float>(), Is.LessThanOrEqualTo(height + 0.5f));
            Assert.That(entry["center"]!["x"]!.Value<float>(), Is.InRange(0f, 1f));
            Assert.That(entry["center"]!["y"]!.Value<float>(), Is.InRange(0f, 1f));
            Assert.That(entry["coverage"]!.Value<float>(), Is.InRange(0f, 1f));
            Assert.That(entry["renderers"]!.Value<int>(), Is.EqualTo(1));
        }

        [Test]
        public void SayThatOcclusionIsNotEvaluated()
        {
            Assert.That(tool.Description, Does.Contain("Occlusion is NOT evaluated"));
        }

        [Test]
        public void KeepTheOutputSchemaInSyncWithTheStructuredPayload()
        {
            // Arrange
            AddCube(600, new Vector3(0f, 0f, 3f));

            // Act
            JObject structured = StructuredOf(Execute(new JObject()));

            // Assert
            McpSchemaAssert.KeysMatch(tool.OutputSchema, structured);
        }

        private Entity AddCube(int crdtId, Vector3 position)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = position;
            cubes.Add(cube);

            return sceneWorld.Create(
                new CRDTEntity(crdtId),
                new PBMeshRenderer { Box = new PBMeshRenderer.Types.BoxMesh() },
                new PrimitiveMeshRendererComponent { MeshRenderer = cube.GetComponent<MeshRenderer>() });
        }

        private McpToolResult Execute(JObject arguments) =>
            tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();

        private static JObject StructuredOf(McpToolResult result) =>
            (JObject)result.Payload["structuredContent"]!;

        private static string TextOf(McpToolResult result) =>
            result.Payload["content"]![0]!["text"]!.Value<string>()!;
    }
}
