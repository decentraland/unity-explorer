using Arch.Core;
using Cysharp.Threading.Tasks;
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
    public class UiDragToolShould
    {
        private World world = null!;
        private GameObject eventSystemGo = null!;
        private UiAutomationServices uiAutomation = null!;
        private UiDragTool tool = null!;

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
            tool = new UiDragTool(uiAutomation);
        }

        [TearDown]
        public void TearDown()
        {
            uiAutomation.Dispose();
            Object.DestroyImmediate(eventSystemGo);
            World.Destroy(world);
        }

        [Test]
        public void DeclareThePathsACallerCanPin()
        {
            var path = (JObject)tool.InputSchema["properties"]!["path"]!;

            CollectionAssert.AreEqual(new[] { "auto", "sdk", "device" }, path["enum"]!.ToObject<string[]>());
        }

        [Test]
        public void FailARequiredSceneUiDragInsteadOfDraggingTheWorld()
        {
            UniTask<McpToolResult> drag = Execute(new JObject { ["path"] = "sdk" });

            // Completing synchronously is the assertion that matters: the virtual-mouse fallback would have
            // installed a gesture request and awaited the simulation instead of reporting the miss.
            Assert.That(drag.Status, Is.EqualTo(UniTaskStatus.Succeeded));

            McpToolResult result = drag.GetAwaiter().GetResult();
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("no running current scene"));
        }

        [Test]
        public void RejectAnUnknownPath()
        {
            McpToolResult result = Execute(new JObject { ["path"] = "semantic" }).GetAwaiter().GetResult();

            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("auto, sdk, device"));
        }

        [Test]
        public void RejectCoordinatesOutsideTheNormalizedRange()
        {
            McpToolResult result = Execute(new JObject { ["toX"] = 1.5f }).GetAwaiter().GetResult();

            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("normalized"));
        }

        private UniTask<McpToolResult> Execute(JObject overrides)
        {
            var arguments = new JObject
            {
                ["fromX"] = 0.5f,
                ["fromY"] = 0.5f,
                ["toX"] = 0.6f,
                ["toY"] = 0.6f,
            };

            foreach (JProperty property in overrides.Properties())
                arguments[property.Name] = property.Value;

            return tool.ExecuteAsync(arguments, CancellationToken.None);
        }
    }
}
