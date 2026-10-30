using Arch.Core;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using ECS.SceneLifeCycle.CurrentScene;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tests
{
    public class GetPlayerStateToolShould
    {
        private World world = null!;
        private GameObject playerGameObject = null!;
        private GetPlayerStateTool tool = null!;

        [SetUp]
        public void Setup()
        {
            world = World.Create();
            playerGameObject = new GameObject(nameof(GetPlayerStateToolShould));

            Entity playerEntity = world.Create(new CharacterTransform(playerGameObject.transform));
            world.Create(new CameraComponent()); // the tool reads the camera mode through CacheCamera()

            tool = new GetPlayerStateTool(world, playerEntity, new ExposedCameraData(), Substitute.For<ICurrentSceneInfo>());
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            Object.DestroyImmediate(playerGameObject);
        }

        [Test]
        public void DeclareAnObjectOutputSchema()
        {
            Assert.That(tool.OutputSchema["type"]!.Value<string>(), Is.EqualTo("object"));
        }

        [Test]
        public void ModelTheAddressAsANullableString()
        {
            var addressType = (JArray)tool.OutputSchema["properties"]!["address"]!["type"]!;
            Assert.That(addressType.ToObject<string[]>(), Is.EqualTo(new[] { "string", "null" }));
        }

        [Test]
        public void ModelTheCameraAsANestedObject()
        {
            JToken camera = tool.OutputSchema["properties"]!["camera"]!;

            Assert.That(camera["type"]!.Value<string>(), Is.EqualTo("object"));
            Assert.That(camera["properties"]!["mode"]!["type"]!.Value<string>(), Is.EqualTo("string"));
        }

        [Test]
        public void KeepTheOutputSchemaInSyncWithTheStructuredPayload()
        {
            // Act
            var structured = (JObject)Execute().Payload["structuredContent"]!;

            // Assert
            McpSchemaAssert.KeysMatch(tool.OutputSchema, structured);
        }

        private McpToolResult Execute() =>
            tool.ExecuteAsync(new JObject(), CancellationToken.None).GetAwaiter().GetResult();
    }
}
