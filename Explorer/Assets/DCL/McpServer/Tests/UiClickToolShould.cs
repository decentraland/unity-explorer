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
    public class UiClickToolShould
    {
        private World world = null!;
        private GameObject eventSystemGo = null!;
        private UiAutomationServices uiAutomation = null!;
        private UiClickTool tool = null!;

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
            tool = new UiClickTool(uiAutomation);
        }

        [TearDown]
        public void TearDown()
        {
            uiAutomation.Dispose();
            Object.DestroyImmediate(eventSystemGo);
            World.Destroy(world);
        }

        [Test]
        public void DeclareTheDeviceSwitchAndTheButtons()
        {
            var properties = (JObject)tool.InputSchema["properties"]!;

            Assert.That(properties["device"]!["type"]!.ToString(), Is.EqualTo("boolean"));
            CollectionAssert.AreEqual(new[] { "left", "right", "middle" }, properties["button"]!["enum"]!.ToObject<string[]>());
        }

        [Test]
        public void RequireACrdtIdForSceneUi()
        {
            McpToolResult result = tool.ExecuteAsync(new JObject { ["stack"] = "sdk" }, CancellationToken.None).GetAwaiter().GetResult();

            AssertError(result, "requires crdtId");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RefuseASceneUiClickWithoutARunningSceneBeforeAnythingIsSent(bool device)
        {
            UniTask<McpToolResult> click = tool.ExecuteAsync(new JObject { ["stack"] = "sdk", ["crdtId"] = 512, ["device"] = device }, CancellationToken.None);

            // Completing synchronously is the assertion: no event was synthesized and no device state was queued.
            Assert.That(click.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            AssertError(click.GetAwaiter().GetResult(), "no running current scene");
        }

        [Test]
        public void RejectAnUnknownButton()
        {
            McpToolResult result = tool.ExecuteAsync(new JObject { ["stack"] = "sdk", ["crdtId"] = 512, ["button"] = "primary" }, CancellationToken.None).GetAwaiter().GetResult();

            AssertError(result, "left");
        }

        [Test]
        public void SayWhatTheDeliveryVerdictMeans()
        {
            Assert.That(tool.Description, Does.Contain("delivered"));
            Assert.That(tool.Description, Does.Contain("not_drained"));
        }

        private static void AssertError(McpToolResult result, string expectedText)
        {
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain(expectedText));
        }
    }
}
