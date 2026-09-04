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

        /// <summary>
        ///     A coordinate that arrives as anything but a number reads as an absent one, and the bare "provide a
        ///     full x/y/z" error then names a cause that is not true — a live run spent several calls attributing
        ///     exactly that. The error has to name the argument and what it actually was.
        /// </summary>
        [Test]
        public void NameTheArgumentWhenACoordinateIsNotANumber()
        {
            McpToolResult result = Execute(new JObject
            {
                ["deltaX"] = 5f,
                ["deltaY"] = 0f,
                ["x"] = 2393f,
                ["y"] = "3.0",
                ["z"] = 2393f,
            });

            string message = result.Payload["content"]![0]!["text"]!.Value<string>()!;

            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            Assert.That(message, Does.Contain("y arrived as string \"3.0\""));
            Assert.That(message, Does.Not.Contain("x arrived"), "the usable coordinates must not be named");
        }

        [Test]
        public void NameNoArgumentWhenTheAimIsSimplyAbsent()
        {
            McpToolResult result = Execute(new JObject { ["deltaX"] = 5f, ["deltaY"] = 0f });

            Assert.That(result.Payload["content"]![0]!["text"]!.Value<string>(), Does.Not.Contain("arrived as"));
        }

        private McpToolResult Execute(JObject arguments) =>
            tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();
    }
}
