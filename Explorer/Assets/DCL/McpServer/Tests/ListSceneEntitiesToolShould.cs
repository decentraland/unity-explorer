using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Debugging;
using SceneRunner.Debugging.Hub;
using System.Collections.Generic;
using System.Threading;

namespace DCL.McpServer.Tests
{
    public class ListSceneEntitiesToolShould
    {
        private const string CURRENT_SCENE = "CURRENT";

        private IWorldInfoHub worldInfoHub = null!;
        private IWorldInfo worldInfo = null!;
        private ListSceneEntitiesTool tool = null!;

        [SetUp]
        public void Setup()
        {
            worldInfo = Substitute.For<IWorldInfo>();
            worldInfoHub = Substitute.For<IWorldInfoHub>();
            worldInfoHub.WorldInfo(CURRENT_SCENE).Returns(worldInfo);
            tool = new ListSceneEntitiesTool(worldInfoHub);
        }

        [Test]
        public void AddAnActionableLineWhenNotEverythingIsShown()
        {
            // Arrange
            worldInfo.EntityIds().Returns(Ids(10));

            // Act
            string text = TextOf(Execute(limit: 3));

            // Assert
            Assert.That(text, Does.Contain("total=10 returned=3"));
            Assert.That(text, Does.Contain("3 of 10 shown"));
        }

        [Test]
        public void OmitTheActionableLineWhenEverythingFits()
        {
            // Arrange
            worldInfo.EntityIds().Returns(Ids(3));

            // Act
            string text = TextOf(Execute(limit: 200));

            // Assert
            Assert.That(text, Does.Contain("total=3 returned=3"));
            Assert.That(text, Does.Not.Contain("shown"));
        }

        [Test]
        public void MirrorTheListingInStructuredContentWhileKeepingTheText()
        {
            // Arrange
            worldInfo.EntityIds().Returns(Ids(10));

            // Act
            McpToolResult result = Execute(limit: 3);

            // Assert — text output is untouched
            Assert.That(TextOf(result), Does.Contain("total=10 returned=3"));

            // Assert — structured mirror carries the same figures
            var structured = (JObject)result.Payload["structuredContent"]!;
            Assert.That(structured["total"]!.Value<int>(), Is.EqualTo(10));
            Assert.That(structured["returned"]!.Value<int>(), Is.EqualTo(3));
            Assert.That(structured["truncated"]!.Value<bool>(), Is.True);
            Assert.That(((JArray)structured["entityIds"]!).ToObject<int[]>(), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void KeepTheOutputSchemaInSyncWithTheStructuredPayload()
        {
            // Arrange
            worldInfo.EntityIds().Returns(Ids(3));

            // Act
            var structured = (JObject)Execute(limit: 200).Payload["structuredContent"]!;

            // Assert
            McpSchemaAssert.KeysMatch(tool.OutputSchema!, structured);
        }

        private McpToolResult Execute(int limit) =>
            tool.ExecuteAsync(new JObject { ["limit"] = limit }, CancellationToken.None).GetAwaiter().GetResult();

        private static IReadOnlyList<int> Ids(int count)
        {
            var ids = new List<int>(count);

            for (var i = 0; i < count; i++)
                ids.Add(i);

            return ids;
        }

        private static string TextOf(McpToolResult result) =>
            result.Payload["content"]![0]!["text"]!.Value<string>()!;
    }
}
