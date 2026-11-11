using Arch.Core;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;
using CameraType = DCL.ECSComponents.CameraType;
using Utility;

namespace DCL.McpServer.Tests
{
    public class ScreenshotToolShould
    {
        private World world = null!;
        private GameObject playerGameObject = null!;
        private ScreenCaptureService capture = null!;
        private ScreenshotTool tool = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            playerGameObject = new GameObject(nameof(ScreenshotToolShould));
            Entity playerEntity = world.Create(new CharacterTransform(playerGameObject.transform));

            capture = new ScreenCaptureService(Substitute.For<ICoroutineRunner>(), world);
            var camera = new IExposedCameraData.Fake(Vector3.zero, Quaternion.identity, CameraType.CtThirdPerson, pointerIsLocked: false);

            tool = new ScreenshotTool(capture, world, playerEntity, camera);
        }

        [TearDown]
        public void TearDown()
        {
            capture.Dispose();
            world.Dispose();
            Object.DestroyImmediate(playerGameObject);
        }

        [Test]
        public void RefuseWhenTheGateIsHeldByAnotherTool()
        {
            // Arrange
            capture.TryAcquire("capture_sequence");

            // Act
            McpToolResult result = Execute(new JObject());

            // Assert
            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]?[0]?["text"]?.Value<string>(), Does.Contain("capture_sequence"));
            Assert.That(capture.BusyOwner, Is.EqualTo("capture_sequence"));
        }

        [Test]
        public void RefuseAnUnknownQuality()
        {
            // Act
            McpToolResult result = Execute(new JObject { ["quality"] = "bmp" });

            // Assert
            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]?[0]?["text"]?.Value<string>(), Does.Contain("quality"));
            Assert.That(capture.BusyOwner, Is.Null);
        }

        [Test]
        public void DeclareTheBlendWaitArgument()
        {
            // Act
            JObject schema = tool.InputSchema;

            // Assert
            Assert.That(schema["properties"]?["waitForCameraBlend"]?["type"]?.Value<string>(), Is.EqualTo("boolean"));
        }

        /// <summary>Only for calls that return before the tool's first await (refusals).</summary>
        private McpToolResult Execute(JObject arguments) =>
            tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();
    }
}
