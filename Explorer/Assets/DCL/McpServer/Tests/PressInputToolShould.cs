using Arch.Core;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.SyntheticInput;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Threading;

namespace DCL.McpServer.Tests
{
    /// <summary>
    ///     Argument diagnosis only: every path past it needs a running simulation, and is covered end-to-end
    ///     against the client instead.
    /// </summary>
    public class PressInputToolShould
    {
        private World world = null!;
        private PressInputTool tool = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            tool = new PressInputTool(new SyntheticInputAgent(world, world.Create()));
        }

        [TearDown]
        public void TearDown()
        {
            World.Destroy(world);
        }

        [Test]
        public void DeclareTheActionValuesInLowerCase()
        {
            string[] values = ((JObject)tool.InputSchema["properties"]!)["action"]!["enum"]!.ToObject<string[]>()!;

            // The wire values are derived from the enum members, and an underscored member is what keeps
            // "action_3" from collapsing into "action3" — the value every doc and recipe spells out.
            CollectionAssert.Contains(values, "primary");
            CollectionAssert.Contains(values, "action_3");
            CollectionAssert.DoesNotContain(values, "PRIMARY");
        }

        /// <summary>
        ///     The values are matched exactly, so a caller that sent "PRIMARY" must not be told it forgot the
        ///     argument: it reads that as "resend the same thing" and loops on the same rejection.
        /// </summary>
        [Test]
        public void NameAnActionValueItDoesNotAccept()
        {
            string message = MessageOf(new JObject { ["action"] = "PRIMARY" });

            Assert.That(message, Does.Contain("string \"PRIMARY\""));
            Assert.That(message, Does.Contain("primary"), "the accepted values must be listed");
            Assert.That(message, Does.Not.Contain("required"), "the argument arrived; it was the value that was unusable");
        }

        [Test]
        public void AskForTheActionWhenItIsAbsent()
        {
            string message = MessageOf(new JObject { ["holdSeconds"] = 1f });

            Assert.That(message, Does.Contain("required"));
            Assert.That(message, Does.Contain("primary"), "the accepted values must be listed");
            Assert.That(message, Does.Not.Contain("arrived"), "nothing arrived to echo back");
        }

        private string MessageOf(JObject arguments)
        {
            McpToolResult result = tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
            return result.Payload["content"]![0]!["text"]!.Value<string>()!;
        }
    }
}
