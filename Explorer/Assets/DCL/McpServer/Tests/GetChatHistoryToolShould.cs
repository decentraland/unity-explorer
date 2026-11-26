using DCL.Chat.History;
using DCL.FeatureFlags;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Threading;

namespace DCL.McpServer.Tests
{
    public class GetChatHistoryToolShould
    {
        private const string PEER = "0x7e27ffde0b2073834424939e5bd2142649c7197c";
        private const string PEER_CHECKSUMMED = "0x7E27FFDe0b2073834424939e5bD2142649c7197c";

        private ChatHistory chatHistory = null!;
        private GetChatHistoryTool tool = null!;

        [SetUp]
        public void SetUp()
        {
            FeatureFlagsConfiguration.Reset();
            OfficialWalletsHelper.Reset();

            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            OfficialWalletsHelper.Initialize(new OfficialWalletsHelper());

            chatHistory = new ChatHistory();
            tool = new GetChatHistoryTool(chatHistory);
        }

        [TearDown]
        public void TearDown()
        {
            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [Test]
        public void ReturnNearbyMessagesOldestFirstWithTheirReactions()
        {
            // Arrange
            ChatChannel nearby = chatHistory.AddOrGetChannel(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);
            ChatMessage first = CreateMessage("first", PEER, 1000);
            ChatMessage second = CreateMessage("second", PEER, 2000);
            nearby.AddMessage(first);
            nearby.AddMessage(second);
            nearby.AddReaction(first.MessageId, 1285, "0xreactor");

            // Act
            JObject payload = Execute(new JObject());

            // Assert
            Assert.That(payload["channel"]?.Value<string>(), Is.EqualTo(ChatChannel.NEARBY_CHANNEL_ID.Id));
            Assert.That(payload["channelType"]?.Value<string>(), Is.EqualTo("nearby"));
            Assert.That(payload["total"]?.Value<int>(), Is.EqualTo(2));

            var messages = (JArray)payload["messages"]!;
            Assert.That(messages.Count, Is.EqualTo(2));
            Assert.That(messages[0]["message"]?.Value<string>(), Is.EqualTo("first"));
            Assert.That(messages[0]["messageId"]?.Value<string>(), Is.EqualTo(first.MessageId));
            Assert.That(messages[0]["sender"]?.Value<string>(), Is.EqualTo(PEER));
            Assert.That(messages[1]["message"]?.Value<string>(), Is.EqualTo("second"));

            var reactions = (JArray)messages[0]["reactions"]!;
            Assert.That(reactions.Count, Is.EqualTo(1));
            Assert.That(reactions[0]["emojiIndex"]?.Value<int>(), Is.EqualTo(1285));
            Assert.That(reactions[0]["count"]?.Value<int>(), Is.EqualTo(1));
            Assert.That(reactions[0]["reactors"]?[0]?.Value<string>(), Is.EqualTo("0xreactor"));
            Assert.That(((JArray)messages[1]["reactions"]!).Count, Is.EqualTo(0));
        }

        [Test]
        public void MarkSystemMessages()
        {
            // Arrange
            ChatChannel nearby = chatHistory.AddOrGetChannel(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);
            nearby.AddMessage(ChatMessage.NewFromSystem("notice"));
            nearby.AddMessage(CreateMessage("hello", PEER, 1000));

            // Act
            JObject payload = Execute(new JObject());

            // Assert
            var messages = (JArray)payload["messages"]!;
            Assert.That(messages[0]["system"]?.Value<bool>(), Is.True);
            Assert.That(messages[1]["system"]?.Value<bool>(), Is.False);
        }

        [Test]
        public void AnswerWithNoMessagesWhileNearbyIsEmpty()
        {
            // Act
            JObject payload = Execute(new JObject());

            // Assert
            Assert.That(payload["total"]?.Value<int>(), Is.EqualTo(0));
            Assert.That(((JArray)payload["messages"]!).Count, Is.EqualTo(0));
            Assert.That(((JArray)payload["channels"]!).Count, Is.EqualTo(0));
        }

        [Test]
        public void ReadADirectConversationByWalletInAnyCase()
        {
            // Arrange
            ChatChannel direct = chatHistory.AddOrGetChannel(new ChatChannel.ChannelId(PEER), ChatChannel.ChatChannelType.USER);
            direct.AddMessage(CreateMessage("psst", PEER, 1000));

            // Act
            JObject payload = Execute(new JObject { ["channel"] = PEER_CHECKSUMMED });

            // Assert
            Assert.That(payload["channel"]?.Value<string>(), Is.EqualTo(PEER));
            Assert.That(payload["channelType"]?.Value<string>(), Is.EqualTo("user"));
            Assert.That(payload["messages"]?[0]?["message"]?.Value<string>(), Is.EqualTo("psst"));
            Assert.That(payload["channels"]?[0]?["id"]?.Value<string>(), Is.EqualTo(PEER));
            Assert.That(payload["channels"]?[0]?["messages"]?.Value<int>(), Is.EqualTo(1));
        }

        [Test]
        public void KeepTheNewestMessagesWithinTheLimit()
        {
            // Arrange
            ChatChannel nearby = chatHistory.AddOrGetChannel(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);

            for (var i = 1; i <= 5; i++)
                nearby.AddMessage(CreateMessage($"message {i}", PEER, 1000 * i));

            // Act
            JObject payload = Execute(new JObject { ["limit"] = 2 });

            // Assert
            var messages = (JArray)payload["messages"]!;
            Assert.That(payload["total"]?.Value<int>(), Is.EqualTo(5));
            Assert.That(messages.Count, Is.EqualTo(2));
            Assert.That(messages[0]["message"]?.Value<string>(), Is.EqualTo("message 4"));
            Assert.That(messages[1]["message"]?.Value<string>(), Is.EqualTo("message 5"));
        }

        [Test]
        public void ReportAChannelThatIsNotInTheHistory()
        {
            // Act
            McpToolResult result = tool.ExecuteAsync(new JObject { ["channel"] = PEER }, CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
        }

        private JObject Execute(JObject arguments)
        {
            McpToolResult result = tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(result.Payload["isError"], Is.Null);
            return JObject.Parse(result.Payload["content"]?[0]?["text"]?.Value<string>() ?? "{}");
        }

        private static ChatMessage CreateMessage(string text, string walletAddress, double timestamp) =>
            new (text, "User", walletAddress, false, walletAddress, timestamp);
    }
}
