using Arch.Core;
using DCL.CharacterCamera;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.SyntheticInput;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Threading;

namespace DCL.McpServer.Tests
{
    public class SweepPointerToolShould
    {
        private World world = null!;
        private SweepPointerTool tool = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            Entity playerEntity = world.Create();

            tool = new SweepPointerTool(new SyntheticInputAgent(world, playerEntity), new ExposedCameraData());
        }

        [TearDown]
        public void TearDown()
        {
            World.Destroy(world);
        }

        [Test]
        public void DeclareTheSweepAndAimArgumentsItInherits()
        {
            var properties = (JObject)tool.InputSchema["properties"]!;

            // The sweep half mirrors camera_look and the aim half mirrors click_entity, so an agent that knows
            // either tool already knows this one.
            foreach (string inherited in new[] { "deltaX", "deltaY", "seconds", "entityId", "x", "y", "z", "sceneId", "button", "timeoutSec" })
                Assert.That(properties[inherited], Is.Not.Null, $"the schema must declare '{inherited}'");

            CollectionAssert.AreEqual(new[] { "pointer", "primary", "secondary" }, properties["button"]!["enum"]!.ToObject<string[]>());
            CollectionAssert.AreEquivalent(new[] { "deltaX", "deltaY" }, tool.InputSchema["required"]!.ToObject<string[]>());
        }

        [Test]
        public void RefuseASweepWithoutAnAimToArmOn()
        {
            McpToolResult result = Execute(new JObject { ["deltaX"] = 5f, ["deltaY"] = 0f });

            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("entityId"));
        }

        [Test]
        public void RefuseASweepThatDoesNotTurnTheCamera()
        {
            McpToolResult result = Execute(new JObject { ["deltaX"] = 0f, ["deltaY"] = 0f, ["entityId"] = 7 });

            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("click_entity"));
        }

        [Test]
        public void RejectAnUnknownButton()
        {
            McpToolResult result = Execute(new JObject { ["deltaX"] = 5f, ["deltaY"] = 0f, ["entityId"] = 7, ["button"] = "middle" });

            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Contain("pointer, primary, secondary"));
        }

        private McpToolResult Execute(JObject arguments) =>
            tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();
    }
}
