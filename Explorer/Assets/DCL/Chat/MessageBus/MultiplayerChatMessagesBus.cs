#nullable enable
using Cysharp.Threading.Tasks;
using DCL.Browser.DecentralandUrls;
using DCL.Chat.History;
using DCL.Chat.MessageBus.Deduplication;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Utilities;
using LiveKit.Proto;
using System;
using System.Threading;

namespace DCL.Chat.MessageBus
{
    public class MultiplayerChatMessagesBus : IChatMessagesBus
    {
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IMessageDeduplication<double> messageDeduplication;
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly ChatMessageFactory messageFactory;
        private readonly string routingUser;

        public event Action<ChatChannel.ChannelId, ChatMessage>? MessageAdded;

        public MultiplayerChatMessagesBus(IMessagePipesHub messagePipesHub,
            ChatMessageFactory messageFactory,
            IMessageDeduplication<double> messageDeduplication,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            DecentralandUrlsSource decentralandUrlsSource)
        {
            this.messagePipesHub = messagePipesHub;
            this.messageDeduplication = messageDeduplication;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.messageFactory = messageFactory;

            // Depending on the selected environment, we send the community messages to one user or another
            string serverEnv = decentralandUrlsSource.Environment == DecentralandEnvironment.Org ? "prd" :
                               decentralandUrlsSource.Environment == DecentralandEnvironment.Zone ? "dev" :
                               "local";
            routingUser = $"message-router-{serverEnv}-0";

            messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Decentraland.Kernel.Comms.Rfc4.Packet.MessageOneofCase.Chat, OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Decentraland.Kernel.Comms.Rfc4.Packet.MessageOneofCase.Chat, OnMessageReceived);
            messagePipesHub.ChatPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Decentraland.Kernel.Comms.Rfc4.Packet.MessageOneofCase.Chat, OnChatPipeMessageReceived);
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private void OnChatPipeMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"Received Chat Message from {receivedMessage.FromWalletId}: {receivedMessage.Payload} with topic: {receivedMessage.Topic}");

            ChatChannel.ChatChannelType channelType = ChatChannel.ChatChannelType.USER;

            if (!string.IsNullOrEmpty(receivedMessage.Topic))
            {
                if (ChatChannel.IsCommunityChannelId(receivedMessage.Topic))
                    channelType = ChatChannel.ChatChannelType.COMMUNITY;

                // groups in the future?
            }

            OnChatAsync(receivedMessage, channelType).Forget();
        }

        private void OnMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            OnChatAsync(receivedMessage).Forget();
        }

        private async UniTaskVoid OnChatAsync(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage, ChatChannel.ChatChannelType channelType = ChatChannel.ChatChannelType.NEARBY)
        {
            using (receivedMessage)
            {
                if (messageDeduplication.TryPass(receivedMessage.FromWalletId, receivedMessage.Payload.Timestamp) == false
                    || IsUserBlockedAndMessagesHidden(receivedMessage.FromWalletId))
                    return;

                ChatChannel.ChannelId parsedChannelId;

                // TODO: Remove this when protobuf is ready
                string topic = receivedMessage.Topic;
                //topic.Split(":from:", 1, StringSplitOptions.RemoveEmptyEntries);
                if (channelType == ChatChannel.ChatChannelType.COMMUNITY)
                {
                    int topicPartLength = "community:c1e1a1b2-1111-4a1b-9111-111111111111".Length;
                    topic = receivedMessage.Topic.Substring(0, topicPartLength);
                }

                switch (channelType)
                {
                    case ChatChannel.ChatChannelType.NEARBY:
                        parsedChannelId = ChatChannel.NEARBY_CHANNEL_ID;
                        break;
                    case ChatChannel.ChatChannelType.COMMUNITY:
                        parsedChannelId = new ChatChannel.ChannelId(topic);
                        break;
                    case ChatChannel.ChatChannelType.USER:
                        parsedChannelId = new ChatChannel.ChannelId(receivedMessage.FromWalletId);
                        break;
                    default:
                        parsedChannelId = new ChatChannel.ChannelId();
                        break;
                }

                // TODO: Remove this when protobuf is ready
                string walletId = receivedMessage.FromWalletId;
                if (channelType == ChatChannel.ChatChannelType.COMMUNITY)
                {
                    int walletPartLength = "community:c1e1a1b2-1111-4a1b-9111-111111111111:from:".Length;
                    walletId = receivedMessage.Topic.Substring(walletPartLength);
                }

                ChatMessage newMessage = await messageFactory.CreateChatMessageAsync(walletId, false, receivedMessage.Payload.Message, null, topic, cancellationTokenSource.Token);

                MessageAdded?.Invoke(parsedChannelId, newMessage);
            }
        }

        private bool IsUserBlockedAndMessagesHidden(string walletAddress) =>
            userBlockingCacheProxy.Configured && userBlockingCacheProxy.Object!.HideChatMessages && userBlockingCacheProxy.Object!.UserIsBlocked(walletAddress);

        public void Send(ChatChannel channel, string message, string origin, string topic)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("ChatMessagesBus is disposed");

            double timestamp = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            switch (channel.ChannelType)
            {
                case ChatChannel.ChatChannelType.NEARBY:
                    SendTo(message, timestamp, messagePipesHub.IslandPipe());
                    SendTo(message, timestamp, messagePipesHub.ScenePipe());
                    break;
                case ChatChannel.ChatChannelType.USER:
                    SendTo(message, timestamp, messagePipesHub.ChatPipe(), channel.Id.Id);
                    break;
                case ChatChannel.ChatChannelType.COMMUNITY:
                    SendTo(message, timestamp, channel.Id.Id, messagePipesHub.ChatPipe(), routingUser);
                    break;
                default:
                    break;
            }

        }

        private void SendTo(string message, double timestamp, IMessagePipe messagePipe, string? recipient = null)
        {
            SendTo(message, timestamp, string.Empty, messagePipe, recipient);
        }

        private void SendTo(string message, double timestamp, string topic, IMessagePipe messagePipe, string? recipient = null)
        {
            MessageWrap<Decentraland.Kernel.Comms.Rfc4.Chat> chat = messagePipe.NewMessage<Decentraland.Kernel.Comms.Rfc4.Chat>(topic);
            if (recipient != null)
                chat.AddSpecialRecipient(recipient);
            chat.Payload.Message = message;
            chat.Payload.Timestamp = timestamp;
            chat.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }
    }
}
