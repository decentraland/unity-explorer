using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat.MessageBus.Deduplication;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Deduplication;
using DCL.SceneBannedUsers;
using DCL.Utilities;
using DCL.Web3.Identities;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Proto;
using LiveKit.Rooms;
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
        private readonly ChatMessageRateLimiter? messageRateLimiter;
        private readonly ChatChannelMessageBuffer? nearbyChannelBuffer;
        private readonly string routingUser;
        private readonly CancellationTokenSource setupExploreSectionsCts = new ();
        private readonly bool isChatMessageRateLimiterEnabled;
        private readonly bool isNearbyChannelBufferEnabled;

        private bool isCommunitiesIncluded;

        public event Action<ChatChannel.ChannelId, ChatChannel.ChatChannelType, ChatMessage>? MessageAdded;

        public MultiplayerChatMessagesBus(IMessagePipesHub messagePipesHub,
            ChatMessageFactory messageFactory,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            DecentralandEnvironment decentralandEnvironment,
            IWeb3IdentityCache identityCache,
            IRoomHub roomHub)
        {
            this.messagePipesHub = messagePipesHub;
            this.messageDeduplication = new MessageDeduplication<double>();
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.identityCache = identityCache;
            this.messageFactory = messageFactory;

            this.isChatMessageRateLimiterEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.CHAT_MESSAGE_RATE_LIMIT);
            if (isChatMessageRateLimiterEnabled)
            {
                messageRateLimiter = new ChatMessageRateLimiter();
                messageRateLimiter.LoadConfigurationFromFeatureFlag();
            }

            isNearbyChannelBufferEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.CHAT_MESSAGE_BUFFER);

            if (isNearbyChannelBufferEnabled)
            {
                nearbyChannelBuffer = new ChatChannelMessageBuffer();
                nearbyChannelBuffer.MessageReleased += OnBufferedMessageReleased;
                roomHub.IslandRoom().ConnectionUpdated += OnIslandConnectionUpdated;
            }

            identityCache.OnIdentityCleared += OnIdentityCleared;

            // Depending on the selected environment, we send the community messages to one user or another
            string serverEnv = decentralandEnvironment switch
                               {
                                   DecentralandEnvironment.Org => "prd",
                                   DecentralandEnvironment.Today => "prd",
                                   DecentralandEnvironment.Zone => "dev",
                                   _ => "local",
                               };

            routingUser = $"message-router-{serverEnv}-0";

            ConfigureMessagePipesHubAsync(setupExploreSectionsCts.Token).Forget();
        }

        public void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();
            setupExploreSectionsCts.SafeCancelAndDispose();
            nearbyChannelBuffer?.Dispose();
        }

        private void OnIslandConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, DisconnectReason? disconnectReason)
        {
            if (connectionUpdate == ConnectionUpdate.Disconnected && disconnectReason == DisconnectReason.ClientInitiated)
                nearbyChannelBuffer!.Reset();
        }

        private async UniTaskVoid ConfigureMessagePipesHubAsync(CancellationToken ct)
        {
            isCommunitiesIncluded = await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct);

            messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Packet.MessageOneofCase.Chat, HandleNearbyPipesMessage);
            messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Packet.MessageOneofCase.Chat, HandleNearbyPipesMessage);
            messagePipesHub.ChatPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Packet.MessageOneofCase.Chat, HandleChatPipeMessage);

            nearbyChannelBuffer?.Start(cancellationTokenSource.Token);
        }

        private void HandleNearbyPipesMessage(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            using (receivedMessage)
            {
                string walletId = receivedMessage.Payload.HasForwardedFrom
                    ? receivedMessage.Payload.ForwardedFrom
                    : receivedMessage.FromWalletId;

                // If the user that sends the message is banned from the current scene, we ignore it
                if (BannedUsersFromCurrentScene.Instance.IsUserBanned(walletId)) return;

                // If the message was already received through the scene or island pipe, we ignore it
                if (messageDeduplication.TryPass(walletId, receivedMessage.Payload.Timestamp) == false) return;

                if (!TryCreateMessage(receivedMessage, out ChatMessage message)) return;

                if (!isNearbyChannelBufferEnabled)
                {
                    MessageAdded?.Invoke(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY, message);
                    return;
                }

                if (!nearbyChannelBuffer!.TryEnqueue(message))
                    ReportHub.Log(ReportCategory.CHAT_MESSAGES, "Failed to enqueue message!");
            }
        }

        private void HandleChatPipeMessage(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            using (receivedMessage)
            {
                if (string.IsNullOrEmpty(receivedMessage.Topic)) return;

                ChatChannel.ChannelId parsedChannelId;
                ChatChannel.ChatChannelType channelType;

                if (ChatChannel.IsCommunityChannelId(receivedMessage.Topic))
                {
                    // If the Communities shape is disabled, ignores the messages
                    if (!isCommunitiesIncluded)
                        return;

                    parsedChannelId = new ChatChannel.ChannelId(receivedMessage.Topic);
                    channelType = ChatChannel.ChatChannelType.COMMUNITY;
                }
                else if (string.Equals(receivedMessage.Topic, identityCache.Identity?.Address, StringComparison.InvariantCultureIgnoreCase))
                {
                    parsedChannelId = new ChatChannel.ChannelId(receivedMessage.FromWalletId);
                    channelType = ChatChannel.ChatChannelType.USER;
                }
                else
                {
                    ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, $"Received a Message with incorrect Topic {receivedMessage.Topic}");
                    return;
                }

                if (TryCreateMessage(receivedMessage, out ChatMessage newMessage))
                    MessageAdded?.Invoke(parsedChannelId, channelType, newMessage);
            }
        }

        private bool TryCreateMessage(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage, out ChatMessage newMessage)
        {
            newMessage = default(ChatMessage);

            string walletId = receivedMessage.Payload.HasForwardedFrom
                ? receivedMessage.Payload.ForwardedFrom
                : receivedMessage.FromWalletId;

            if (IsUserBlockedAndMessagesHidden(walletId)) return false;

            if (isChatMessageRateLimiterEnabled && !messageRateLimiter!.TryAllow(walletId)) return false;

            newMessage = messageFactory.CreateChatMessage(walletId, false, receivedMessage.Payload.Message, null, receivedMessage.Payload.Timestamp);
            return true;
        }

        private bool IsUserBlockedAndMessagesHidden(string walletAddress) =>
            userBlockingCacheProxy.Configured && userBlockingCacheProxy.Object!.HideChatMessages && userBlockingCacheProxy.Object!.UserIsBlocked(walletAddress);

        private void OnBufferedMessageReleased(ChatMessage message)
        {
            MessageAdded?.Invoke(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY, message);
        }

        private void OnIdentityCleared()
        {
            nearbyChannelBuffer?.Reset();
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
