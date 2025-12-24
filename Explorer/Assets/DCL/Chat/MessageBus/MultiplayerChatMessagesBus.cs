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
using DCL.SceneBannedUsers;
using DCL.Utilities;
using DCL.Web3.Identities;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Proto;
using System;
using System.Threading;
using Utility;
using ChatMessage = DCL.Chat.History.ChatMessage;

namespace DCL.Chat.MessageBus
{
    public class MultiplayerChatMessagesBus : IChatMessagesBus
    {
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IMessageDeduplication<double> messageDeduplication;
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly IWeb3IdentityCache identityCache;
        private readonly ChatMessageFactory messageFactory;
        private readonly ChatMessageRateLimiter messageRateLimiter;
        private readonly ChatChannelMessageBuffer nearbyChannelBuffer;
        private readonly string routingUser;
        private bool isCommunitiesIncluded;
        private readonly CancellationTokenSource setupExploreSectionsCts = new ();

        public event Action<ChatChannel.ChannelId, ChatChannel.ChatChannelType, ChatMessage>? MessageAdded;

        public MultiplayerChatMessagesBus(IMessagePipesHub messagePipesHub,
            ChatMessageFactory messageFactory,
            IMessageDeduplication<double> messageDeduplication,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            DecentralandEnvironment decentralandEnvironment,
            IWeb3IdentityCache identityCache)
        {
            this.messagePipesHub = messagePipesHub;
            this.messageDeduplication = messageDeduplication;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.identityCache = identityCache;
            this.messageFactory = messageFactory;
            messageRateLimiter = new ChatMessageRateLimiter();
            messageRateLimiter.LoadConfigurationFromFeatureFlag();
            nearbyChannelBuffer = new ChatChannelMessageBuffer();
            nearbyChannelBuffer.MessageReleased += OnBufferedMessageReleased;

            identityCache.OnIdentityCleared += OnIdentityCleared;

            // Depending on the selected environment, we send the community messages to one user or another
            string serverEnv = decentralandEnvironment switch
                               {
                                   DecentralandEnvironment.Org => "prd",
                                   DecentralandEnvironment.Today => "prd",
                                   DecentralandEnvironment.Zone => "dev",
                                   _ => "local"
                               };
            routingUser = $"message-router-{serverEnv}-0";

            ConfigureMessagePipesHubAsync(setupExploreSectionsCts.Token).Forget();
        }

        private async UniTaskVoid ConfigureMessagePipesHubAsync(CancellationToken ct)
        {
            isCommunitiesIncluded = await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct);

            messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Packet.MessageOneofCase.Chat, OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Packet.MessageOneofCase.Chat, OnMessageReceived);
            messagePipesHub.ChatPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Packet.MessageOneofCase.Chat, OnChatPipeMessageReceived);

            nearbyChannelBuffer.Start(cancellationTokenSource.Token);
        }

        public void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();
            setupExploreSectionsCts.SafeCancelAndDispose();
            nearbyChannelBuffer.Dispose();
        }

        private void OnChatPipeMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            ChatChannel.ChatChannelType channelType = ChatChannel.ChatChannelType.USER;

            if (!string.IsNullOrEmpty(receivedMessage.Topic))
            {
                if (ChatChannel.IsCommunityChannelId(receivedMessage.Topic))
                    channelType = ChatChannel.ChatChannelType.COMMUNITY;
                else if (receivedMessage.Topic != identityCache.Identity?.Address)
                {
                    ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, $"Received a Private Message with incorrect Topic {receivedMessage.Topic}");
                    return;
                }
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
                if (!messageRateLimiter.TryAllow(receivedMessage.FromWalletId)) return;

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

                // If the user that sends the message is banned from the current scene, we ignore the message
                if (channelType == ChatChannel.ChatChannelType.NEARBY && BannedUsersFromCurrentScene.Instance.IsUserBanned(walletId))
                    return;

                if (channelType == ChatChannel.ChatChannelType.NEARBY)
                {
                    if (!nearbyChannelBuffer.HasCapacity())
                    {
                        ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, "Discarded message, queue full!");
                        return;
                    }

                    ChatMessage newMessage = messageFactory.CreateChatMessage(walletId, false, receivedMessage.Payload.Message, null, receivedMessage.Payload.Timestamp);
                    if (!nearbyChannelBuffer.TryEnqueue(newMessage))
                        ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, "Failed to enqueue message!");
                }
                else
                {
                    ChatMessage newMessage = messageFactory.CreateChatMessage(walletId, false, receivedMessage.Payload.Message, null, receivedMessage.Payload.Timestamp);
                    MessageAdded?.Invoke(parsedChannelId, channelType, newMessage);
                }
            }
        }

        private bool IsUserBlockedAndMessagesHidden(string walletAddress) =>
            userBlockingCacheProxy.Configured && userBlockingCacheProxy.Object!.HideChatMessages && userBlockingCacheProxy.Object!.UserIsBlocked(walletAddress);

        private void OnBufferedMessageReleased(ChatMessage message)
        {
            MessageAdded?.Invoke(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY, message);
        }

        private void OnIdentityCleared()
        {
            nearbyChannelBuffer.Reset();
        }

        public void Send(ChatChannel channel, string message, ChatMessageOrigin origin, double timestamp)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("ChatMessagesBus is disposed");

            switch (channel.ChannelType)
            {
                case ChatChannel.ChatChannelType.NEARBY:
                    SendTo(message, timestamp, messagePipesHub.IslandPipe());
                    SendTo(message, timestamp, messagePipesHub.ScenePipe());
                    break;
                case ChatChannel.ChatChannelType.USER:
                    SendTo(message, timestamp, channel.Id.Id, messagePipesHub.ChatPipe(), channel.Id.Id);
                    break;
                case ChatChannel.ChatChannelType.COMMUNITY:
                    SendTo(message, timestamp, channel.Id.Id, messagePipesHub.ChatPipe(), routingUser);
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
