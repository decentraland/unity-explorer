using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.RealmNavigation;
using DCL.Utilities;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.McpServer.Tests
{
    public class SendChatToolShould
    {
        private const string WALLET = "0x5Fd69ad6170A1cBE7B15a31C588a60FD1a689E80";

        private RecordingBus bus = null!;
        private ReactiveProperty<LoadingStatus.LoadingStage> loadingStage = null!;
        private SendChatTool tool = null!;

        [SetUp]
        public void SetUp()
        {
            bus = new RecordingBus();
            loadingStage = new ReactiveProperty<LoadingStatus.LoadingStage>(LoadingStatus.LoadingStage.Completed);

            ILoadingStatus loadingStatus = Substitute.For<ILoadingStatus>();
            loadingStatus.CurrentStage.Returns(loadingStage);

            tool = new SendChatTool(bus, loadingStatus);
        }

        [Test]
        public void SendToNearbyWithoutARecipient()
        {
            // Act
            McpToolResult result = Execute(new JObject { ["message"] = "hello" });

            // Assert
            Assert.That(result.Payload["isError"], Is.Null);
            Assert.That(bus.Sent, Has.Count.EqualTo(1));
            Assert.That(bus.Sent[0].Channel.ChannelType, Is.EqualTo(ChatChannel.ChatChannelType.NEARBY));
            Assert.That(bus.Sent[0].Message, Is.EqualTo("hello"));
        }

        [Test]
        public void SendADirectMessageToTheLowerCasedWallet()
        {
            // Act
            McpToolResult result = Execute(new JObject { ["message"] = "hello", ["to"] = WALLET });

            // Assert — the channel id is the wallet as incoming direct messages name it
            Assert.That(result.Payload["isError"], Is.Null);
            Assert.That(bus.Sent, Has.Count.EqualTo(1));
            Assert.That(bus.Sent[0].Channel.ChannelType, Is.EqualTo(ChatChannel.ChatChannelType.USER));
            Assert.That(bus.Sent[0].Channel.Id.Id, Is.EqualTo(WALLET.ToLowerInvariant()));
            Assert.That(bus.Sent[0].Message, Is.EqualTo("hello"));
            Assert.That(TextOf(result), Does.Contain(WALLET.ToLowerInvariant()));
        }

        [TestCase("0x123")]
        [TestCase("5Fd69ad6170A1cBE7B15a31C588a60FD1a689E80ab")]
        [TestCase("0x5Fd69ad6170A1cBE7B15a31C588a60FD1a689E8Z")]
        public void RefuseARecipientThatIsNotAWallet(string to)
        {
            // Act
            McpToolResult result = Execute(new JObject { ["message"] = "hello", ["to"] = to });

            // Assert
            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(bus.Sent, Is.Empty);
        }

        [Test]
        public void RefuseACommandAsADirectMessage()
        {
            // Act — the bus would run it as a chat command instead of sending it to the player
            McpToolResult result = Execute(new JObject { ["message"] = "/goto 0,0", ["to"] = WALLET });

            // Assert
            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(bus.Sent, Is.Empty);
        }

        [Test]
        public void RefuseToSendWhileTheClientIsLoading()
        {
            // Arrange — the chat bus drops every message until loading completes
            loadingStage.Value = LoadingStatus.LoadingStage.Init;

            // Act
            McpToolResult nearby = Execute(new JObject { ["message"] = "hello" });
            McpToolResult direct = Execute(new JObject { ["message"] = "hello", ["to"] = WALLET });

            // Assert
            Assert.That(nearby.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(direct.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(bus.Sent, Is.Empty);
        }

        [Test]
        public void DescribeTheOptionalRecipient()
        {
            // Act
            JObject schema = tool.InputSchema;

            // Assert
            Assert.That(schema["properties"]?["to"]?["type"]?.Value<string>(), Is.EqualTo("string"));
            Assert.That(schema["properties"]?["message"]?["type"]?.Value<string>(), Is.EqualTo("string"));
        }

        private McpToolResult Execute(JObject arguments) =>
            tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();

        private static string TextOf(McpToolResult result) =>
            result.Payload["content"]?[0]?["text"]?.Value<string>() ?? string.Empty;

        private sealed class RecordingBus : IChatMessagesBus
        {
            public readonly List<(ChatChannel Channel, string Message)> Sent = new ();

            public event Action<ChatChannel.ChannelId, ChatChannel.ChatChannelType, ChatMessage> MessageAdded = delegate { };

            public void Send(ChatChannel channel, string message, ChatMessageOrigin origin, double timestamp) =>
                Sent.Add((channel, message));

            public void Dispose() { }
        }
    }
}
