using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Debugging;
using SceneRunner.Debugging.Hub;
using System.Threading;

namespace DCL.McpServer.Tests
{
    public class GetEntityDetailsToolShould
    {
        private const string CURRENT_SCENE = "CURRENT";

        private IWorldInfoHub worldInfoHub = null!;
        private IWorldInfo worldInfo = null!;
        private GetEntityDetailsTool tool = null!;

        [SetUp]
        public void Setup()
        {
            worldInfo = Substitute.For<IWorldInfo>();
            worldInfoHub = Substitute.For<IWorldInfoHub>();
            worldInfoHub.WorldInfo(CURRENT_SCENE).Returns(worldInfo);
            tool = new GetEntityDetailsTool(worldInfoHub);
        }

        [Test]
        public void ReturnTheDumpWholeWhenItFitsTheBudget()
        {
            // Arrange
            const string DUMP = "Components of entity 5, total count: 1\n1) PBTransform";
            worldInfo.EntityComponentsInfo(5).Returns(DUMP);

            // Act
            string text = TextOf(Execute(5));

            // Assert
            Assert.That(text, Is.EqualTo(DUMP));
            Assert.That(text, Does.Not.Contain("truncated"));
        }

        [Test]
        public void TruncateWithANoteWhenTheDumpExceedsTheBudget()
        {
            // Arrange
            string dump = new ('x', 20000);
            worldInfo.EntityComponentsInfo(5).Returns(dump);

            // Act
            string text = TextOf(Execute(5));

            // Assert
            Assert.That(text.Length, Is.LessThan(dump.Length));
            Assert.That(text, Does.Contain($"output truncated at 8000/{dump.Length} chars"));
        }

        [Test]
        public void ErrorWhenNoSceneWorldIsFound()
        {
            // Arrange
            worldInfoHub.WorldInfo(CURRENT_SCENE).Returns((IWorldInfo?)null);

            // Act
            McpToolResult result = Execute(5);

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
        }

        [Test]
        public void ErrorWhenEntityIdIsMissing()
        {
            // Act
            McpToolResult result = tool.ExecuteAsync(new JObject(), CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
        }

        private McpToolResult Execute(int entityId) =>
            tool.ExecuteAsync(new JObject { ["entityId"] = entityId }, CancellationToken.None).GetAwaiter().GetResult();

        private static string TextOf(McpToolResult result) =>
            result.Payload["content"]![0]!["text"]!.Value<string>()!;
    }
}
