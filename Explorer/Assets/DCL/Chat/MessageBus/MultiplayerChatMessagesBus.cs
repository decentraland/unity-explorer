#nullable enable
using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat.MessageBus.Deduplication;
using DCL.Communities;
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
using Utility;

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
        private bool isCommunitiesIncluded;
        private CancellationTokenSource setupExploreSectionsCts;

        public event Action<ChatChannel.ChannelId, ChatChannel.ChatChannelType, ChatMessage>? MessageAdded;

        public MultiplayerChatMessagesBus(IMessagePipesHub messagePipesHub,
            ChatMessageFactory messageFactory,
            IMessageDeduplication<double> messageDeduplication,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            IDecentralandUrlsSource decentralandUrlsSource)
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

            setupExploreSectionsCts = setupExploreSectionsCts.SafeRestart();
            ConfigureMessagePipesHubAsync(setupExploreSectionsCts.Token).Forget();
        }

        private async UniTaskVoid ConfigureMessagePipesHubAsync(CancellationToken ct)
        {
            isCommunitiesIncluded = await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct);
            if (isCommunitiesIncluded)
            {
                messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Decentraland.Kernel.Comms.Rfc4.Packet.MessageOneofCase.Chat, OnMessageReceived);
                messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Decentraland.Kernel.Comms.Rfc4.Packet.MessageOneofCase.Chat, OnMessageReceived);
                messagePipesHub.ChatPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Decentraland.Kernel.Comms.Rfc4.Packet.MessageOneofCase.Chat, OnChatPipeMessageReceived);
            }
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            setupExploreSectionsCts.SafeCancelAndDispose();
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

            OnChat(receivedMessage, channelType);
        }

        private void OnMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            OnChat(receivedMessage);
        }

        private void OnChat(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage, ChatChannel.ChatChannelType channelType = ChatChannel.ChatChannelType.NEARBY)
        {
            using (receivedMessage)
            {
                // If the Communities shape is disabled, ignores the community conversation messages
                if(!isCommunitiesIncluded && channelType == ChatChannel.ChatChannelType.COMMUNITY)
                    return;

                if (messageDeduplication.TryPass(receivedMessage.FromWalletId, receivedMessage.Payload.Timestamp) == false
                    || IsUserBlockedAndMessagesHidden(receivedMessage.FromWalletId))
                    return;

                ChatChannel.ChannelId parsedChannelId;

                switch (channelType)
                {
                    case ChatChannel.ChatChannelType.NEARBY:
                        parsedChannelId = ChatChannel.NEARBY_CHANNEL_ID;
                        break;
                    case ChatChannel.ChatChannelType.COMMUNITY:
                        parsedChannelId = new ChatChannel.ChannelId(receivedMessage.Topic);
                        break;
                    case ChatChannel.ChatChannelType.USER:
                        parsedChannelId = new ChatChannel.ChannelId(receivedMessage.FromWalletId);
                        break;
                    default:
                        parsedChannelId = new ChatChannel.ChannelId();
                        break;
                }

                string walletId = receivedMessage.Payload.HasForwardedFrom ? receivedMessage.Payload.ForwardedFrom
                                                                           : receivedMessage.FromWalletId;

                ChatMessage newMessage = messageFactory.CreateChatMessage(walletId, false, receivedMessage.Payload.Message, null, receivedMessage.Topic);

                MessageAdded?.Invoke(parsedChannelId, channelType, newMessage);
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
            chat.Payload.ClearForwardedFrom(); // It has to be reset in every use. To be filled by the server.
            chat.Payload.Message = message;
            chat.Payload.Timestamp = timestamp;
            chat.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }
    }
}
