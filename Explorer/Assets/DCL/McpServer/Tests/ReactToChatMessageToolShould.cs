using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.ChatReactions.Tests;
using DCL.Chat.History;
using DCL.FeatureFlags;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.Web3.Identities;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Threading;

namespace DCL.McpServer.Tests
{
    public class ReactToChatMessageToolShould
    {
        private const int EMOJI = 1285;

        private ChatHistory chatHistory = null!;
        private FakeReactionBus reactionBus = null!;
        private ChatMessageReactionService service = null!;
        private MessageReactionServiceSlot slot = null!;
        private ReactToChatMessageTool tool = null!;
        private ChatChannel nearby = null!;
        private ChatMessage message;

        [SetUp]
        public void SetUp()
        {
            FeatureFlagsConfiguration.Reset();
            OfficialWalletsHelper.Reset();

            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            OfficialWalletsHelper.Initialize(new OfficialWalletsHelper());

            chatHistory = new ChatHistory();
            reactionBus = new FakeReactionBus();
            service = new ChatMessageReactionService(reactionBus, chatHistory, new IWeb3IdentityCache.Fake(new IWeb3Identity.Random()));

            slot = new MessageReactionServiceSlot();
            slot.Attach(service);
            tool = new ReactToChatMessageTool(chatHistory, slot);

            nearby = chatHistory.AddOrGetChannel(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);
            message = new ChatMessage("hello", "User", "0xsender", false, "0xsender", 1000);
            nearby.AddMessage(message);
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [Test]
        public void AddTheReactionAndSendItToTheChannel()
        {
            // Act
            McpToolResult result = Execute(new JObject { ["messageId"] = message.MessageId, ["emojiIndex"] = EMOJI });

            // Assert
            Assert.That(result.Payload["isError"], Is.Null);
            JObject payload = JsonOf(result);
            Assert.That(payload["state"]?.Value<string>(), Is.EqualTo("added"));
            Assert.That(payload["reactors"]?.Value<int>(), Is.EqualTo(1));
            Assert.That(payload["channel"]?.Value<string>(), Is.EqualTo(ChatChannel.NEARBY_CHANNEL_ID.Id));

            Assert.That(nearby.GetReactions(message.MessageId)?.GetReactors(EMOJI)?.Count, Is.EqualTo(1));
            Assert.That(reactionBus.MessageSends.Count, Is.EqualTo(1));
            Assert.That(reactionBus.MessageSends[0].EmojiIndex, Is.EqualTo(EMOJI));
            Assert.That(reactionBus.MessageSends[0].MessageId, Is.EqualTo(message.MessageId));
            Assert.That(reactionBus.MessageSends[0].Routing.ChannelType, Is.EqualTo(ChatChannel.ChatChannelType.NEARBY));
        }

        [Test]
        public void RemoveTheReactionOnTheSecondCall()
        {
            // Arrange
            Execute(new JObject { ["messageId"] = message.MessageId, ["emojiIndex"] = EMOJI });

            // Act
            McpToolResult result = Execute(new JObject { ["messageId"] = message.MessageId, ["emojiIndex"] = EMOJI });

            // Assert — the removal travels as the bitwise complement of the index
            Assert.That(result.Payload["isError"], Is.Null);
            JObject payload = JsonOf(result);
            Assert.That(payload["state"]?.Value<string>(), Is.EqualTo("removed"));
            Assert.That(payload["reactors"]?.Value<int>(), Is.EqualTo(0));
            Assert.That(nearby.GetReactions(message.MessageId), Is.Null);
            Assert.That(reactionBus.MessageSends.Count, Is.EqualTo(2));
            Assert.That(reactionBus.MessageSends[1].EmojiIndex, Is.EqualTo(~EMOJI));
        }

        [Test]
        public void RefuseWhileReactionsAreNotReady()
        {
            // Arrange
            slot.Detach();

            // Act
            McpToolResult result = Execute(new JObject { ["messageId"] = message.MessageId, ["emojiIndex"] = EMOJI });

            // Assert
            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(reactionBus.MessageSends.Count, Is.EqualTo(0));
        }

        [Test]
        public void RefuseAMessageThatIsNotInTheHistory()
        {
            // Act
            McpToolResult result = Execute(new JObject { ["messageId"] = "0xnobody:1", ["emojiIndex"] = EMOJI });

            // Assert
            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(reactionBus.MessageSends.Count, Is.EqualTo(0));
        }

        [Test]
        public void RefuseAMissingOrNegativeEmojiIndex()
        {
            // Act
            McpToolResult missing = Execute(new JObject { ["messageId"] = message.MessageId });
            McpToolResult negative = Execute(new JObject { ["messageId"] = message.MessageId, ["emojiIndex"] = -1 });

            // Assert
            Assert.That(missing.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(negative.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(reactionBus.MessageSends.Count, Is.EqualTo(0));
        }

        private McpToolResult Execute(JObject arguments) =>
            tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();

        private static JObject JsonOf(McpToolResult result) =>
            JObject.Parse(result.Payload["content"]?[0]?["text"]?.Value<string>() ?? "{}");
    }
}
