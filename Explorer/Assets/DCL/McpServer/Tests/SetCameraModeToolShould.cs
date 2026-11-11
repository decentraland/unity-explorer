using Arch.Core;
using DCL.CharacterCamera;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tests
{
    public class SetCameraModeToolShould
    {
        private const string SCENE_NAME = "showcase";
        private const int AREA_CRDT_ID = 540;

        private World world = null!;
        private Entity cameraEntity;
        private SetCameraModeTool tool = null!;

        [SetUp]
        public void Setup()
        {
            world = World.Create();

            var camera = new CameraComponent { Mode = CameraMode.ThirdPerson };
            camera.AddCameraInputLock();
            cameraEntity = world.Create(camera);

            tool = new SetCameraModeTool(world, new ExposedCameraData());
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void NameTheAreaHoldingTheLock()
        {
            // Arrange
            world.Add(cameraEntity, new CameraModeAreaLock
            {
                SceneName = SCENE_NAME,
                SceneBaseParcel = new Vector2Int(149, 149),
                CrdtEntityId = AREA_CRDT_ID,
                Mode = CameraMode.FirstPerson,
                AreaSize = new Vector3(3f, 3f, 3f),
                ActiveAreaCount = 1,
            });

            // Act
            McpToolResult result = Execute("third_person");

            // Assert
            string text = ErrorText(result);
            Assert.That(text, Does.Contain(SCENE_NAME));
            Assert.That(text, Does.Contain("149,149"));
            Assert.That(text, Does.Contain($"crdtId {AREA_CRDT_ID}"));
            Assert.That(text, Does.Contain("forces first_person"));
            Assert.That(text, Does.Not.Contain("nested"));
            Assert.That(world.Get<CameraComponent>(cameraEntity).Mode, Is.EqualTo(CameraMode.ThirdPerson));
        }

        [Test]
        public void SayHowManyAreasNest()
        {
            // Arrange
            world.Add(cameraEntity, new CameraModeAreaLock { SceneName = SCENE_NAME, CrdtEntityId = AREA_CRDT_ID, ActiveAreaCount = 2 });

            // Act
            McpToolResult result = Execute("third_person");

            // Assert
            Assert.That(ErrorText(result), Does.Contain("2 areas nested"));
        }

        [Test]
        public void FallBackToTheGenericTextWithoutAMirroredLock()
        {
            // Act
            McpToolResult result = Execute("first_person");

            // Assert
            Assert.That(ErrorText(result), Does.Contain("locked by the scene (CameraModeArea"));
        }

        [Test]
        public void RefuseWhileASceneVirtualCameraIsActive()
        {
            // Arrange
            world.Set(cameraEntity, new CameraComponent { Mode = CameraMode.SdkCamera });

            // Act
            McpToolResult result = Execute("first_person");

            // Assert
            Assert.That(ErrorText(result), Does.Contain("scene virtual camera"));
        }

        /// <summary>Only for calls refused before the tool's frame delay, which complete synchronously.</summary>
        private McpToolResult Execute(string mode) =>
            tool.ExecuteAsync(new JObject { ["mode"] = mode }, CancellationToken.None).GetAwaiter().GetResult();

        private static string ErrorText(McpToolResult result)
        {
            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
            return result.Payload["content"]?[0]?["text"]?.Value<string>() ?? string.Empty;
        }
    }
}
