using Arch.Core;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.SyntheticInput.UiSimulation;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.McpServer.Tests
{
    public class UiHoverToolShould
    {
        private World world = null!;
        private GameObject eventSystemGo = null!;
        private UiAutomationServices uiAutomation = null!;
        private UiHoverTool tool = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            Entity playerEntity = world.Create();

            eventSystemGo = new GameObject("test-event-system");
            var eventSystem = eventSystemGo.AddComponent<EventSystem>();

            var scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(null));

            uiAutomation = new UiAutomationServices(world, playerEntity, eventSystem, scenesCache);
            tool = new UiHoverTool(uiAutomation);
        }

        [TearDown]
        public void TearDown()
        {
            uiAutomation.Dispose();
            Object.DestroyImmediate(eventSystemGo);
            World.Destroy(world);
        }

        [Test]
        public void RefuseALeaveThatAlsoNamesAnAddress()
        {
            // Act
            McpToolResult result = Execute(new JObject { ["leave"] = true, ["stack"] = "sdk", ["crdtId"] = 512 });

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("takes no address").And.Contain("'stack'"));
        }

        [Test]
        public void SayNothingWasLeftWhenNoHoverIsParked()
        {
            // Act
            McpToolResult result = Execute(new JObject { ["leave"] = true });

            // Assert
            JObject json = JObject.Parse(result.Payload["content"]![0]!["text"]!.Value<string>()!);
            Assert.That(json["ok"]!.Value<bool>(), Is.True);
            Assert.That(json["left"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(json["info"]!.Value<string>(), Does.Contain("no hover was parked"));
        }

        [Test]
        public void RefuseADeviceHoverOnSceneUi()
        {
            // Act
            McpToolResult result = Execute(new JObject { ["stack"] = "sdk", ["crdtId"] = 512, ["device"] = true });

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("client UI"));
        }

        private McpToolResult Execute(JObject arguments) =>
            tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();
    }
}
