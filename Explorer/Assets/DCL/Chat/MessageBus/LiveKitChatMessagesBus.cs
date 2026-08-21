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
using DCL.Web3;
using DCL.Web3.Identities;
using Decentraland.Kernel.Comms.Rfc4;
using DCL.LiveKit.Public;
using LiveKit.Rooms;
using System;
using System.Threading;
using Utility;
using ChatMessage = DCL.Chat.History.ChatMessage;

namespace DCL.Chat.MessageBus
{
    public class LiveKitChatMessagesBus : IChatMessagesBus
    {
        // Hard ceiling on the dedup stamps retained per period. A flood of distinct timestamps
        // restarts the window instead of growing it, bounding the memory a sender can make this
        // cache hold.
        private const int MAX_DEDUP_ENTRIES = 2048;

        private readonly IMessagePipesHub messagePipesHub;
        private readonly IMessageDeduplication<double> messageDeduplication;
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly IUserBlockingCache userBlockingCache;
        private readonly IWeb3IdentityCache identityCache;
        private readonly ChatMessageFactory messageFactory;
        private readonly ChatMessageRateLimiter? messageRateLimiter;
        private readonly ChatChannelMessageBuffer? nearbyChannelBuffer;
        private readonly string routingUser;
        private readonly CancellationTokenSource setupExploreSectionsCts = new ();
        private readonly bool isChatMessageRateLimiterEnabled;
        private readonly bool isNearbyChannelBufferEnabled;
        private readonly bool isPrivateChatRequiresTopicEnabled;

        private bool isCommunitiesIncluded;

        public event Action<ChatChannel.ChannelId, ChatChannel.ChatChannelType, ChatMessage>? MessageAdded;

        public LiveKitChatMessagesBus(IMessagePipesHub messagePipesHub,
            ChatMessageFactory messageFactory,
            IUserBlockingCache userBlockingCache,
            DecentralandEnvironment decentralandEnvironment,
            IWeb3IdentityCache identityCache,
            IRoomHub roomHub)
        {
            this.messagePipesHub = messagePipesHub;
            messageDeduplication = new MessageDeduplication<double>(MAX_DEDUP_ENTRIES);
            this.userBlockingCache = userBlockingCache;
            this.identityCache = identityCache;
            this.messageFactory = messageFactory;

            isChatMessageRateLimiterEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.ChatMessageRateLimit);

            if (isChatMessageRateLimiterEnabled)
            {
                messageRateLimiter = new ChatMessageRateLimiter();
                messageRateLimiter.LoadConfigurationFromFeatureFlag();
            }

            isNearbyChannelBufferEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.ChatMessageBuffer);

            if (isNearbyChannelBufferEnabled)
            {
                nearbyChannelBuffer = new ChatChannelMessageBuffer();
                nearbyChannelBuffer.MessageReleased += OnBufferedMessageReleased;
                roomHub.IslandRoom().ConnectionUpdated += OnIslandConnectionUpdated;
            }

            isPrivateChatRequiresTopicEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.PrivateChatRequiresTopic);

            identityCache.OnIdentityCleared += OnIdentityCleared;

            // Depending on the selected environment, we send the community messages to one user or another
            string serverEnv = decentralandEnvironment switch
                               {
                                   DecentralandEnvironment.Org => "prd",
                                   DecentralandEnvironment.Today => "prd",
                                   DecentralandEnvironment.Zone => "dev",

                                   // A --base-domain deployment is treated as a non-production stack, like zone: its
                                   // comms-message-sfu has to join as message-router-dev-0 for relayed messages to
                                   // authenticate. Explicit, so it is not silently routed as a local dev server.
                                   DecentralandEnvironment.Custom => "dev",
                                   _ => "local",
                               };

            // Must match the participant identity that comms-message-sfu joins the room under: it is the
            // only signal that authenticates a relayed message's ForwardedFrom stamp. If the two ever
            // diverge, relayed community messages are attributed to the router instead of their sender.
            routingUser = $"message-router-{serverEnv}-0";

            ConfigureMessagePipesHubAsync(setupExploreSectionsCts.Token).Forget();
        }

        public void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();
            setupExploreSectionsCts.SafeCancelAndDispose();
            nearbyChannelBuffer?.Dispose();
        }

        private void OnIslandConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, LKDisconnectReason? disconnectReason)
        {
            //We clear the buffer if we disconnect from the island, so we won't keep receiving messages from that nearby area.
            if (connectionUpdate == ConnectionUpdate.Disconnected && disconnectReason == LKDisconnectReason.UnknownReason)
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
                // The island and scene pipes are peer-to-peer, so the message router is never in their
                // path and a populated ForwardedFrom can only have been set by the publishing peer.
                // Keying on it would let any peer publish under an arbitrary wallet and rotate the
                // value to get a fresh slot for each of the checks below.
                string walletId = receivedMessage.FromWalletId;

                // If the user that sends the message is banned from the current scene, we ignore it
                if (RoomMetadataCurrentScene.Instance.IsUserBanned(walletId)) return;

                // If the message was already received through the scene or island pipe, we ignore it
                if (messageDeduplication.TryPass(walletId, receivedMessage.Payload.Timestamp) == false) return;

                if (!TryCreateMessage(receivedMessage, walletId, out ChatMessage message)) return;

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
                else if (!isPrivateChatRequiresTopicEnabled || string.Equals(receivedMessage.Topic, identityCache.Identity?.Address, StringComparison.InvariantCultureIgnoreCase))
                {
                    parsedChannelId = new ChatChannel.ChannelId(receivedMessage.FromWalletId);
                    channelType = ChatChannel.ChatChannelType.USER;
                }
                else
                {
                    ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, $"Received a Message with incorrect Topic {receivedMessage.Topic}");
                    return;
                }

                string walletId = ResolveChatPipeSenderWalletId(receivedMessage);

                if (TryCreateMessage(receivedMessage, walletId, out ChatMessage newMessage))
                    MessageAdded?.Invoke(parsedChannelId, channelType, newMessage);
            }
        }

        /// <summary>
        /// Resolves the author of a message received on the chat pipe.
        /// </summary>
        /// <remarks>
        /// ForwardedFrom is stamped only by the trusted message router, which relays community messages
        /// under the <see cref="routingUser"/> identity. Honoring it from any other sender would let a
        /// peer publish under an arbitrary wallet, since the field is an ordinary payload string while
        /// FromWalletId is the LiveKit-authenticated participant identity.
        /// </remarks>
        private string ResolveChatPipeSenderWalletId(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            if (receivedMessage.Payload.HasForwardedFrom
                && string.Equals(receivedMessage.FromWalletId, routingUser, StringComparison.OrdinalIgnoreCase)
                && Web3Address.IsValidWalletAddress(receivedMessage.Payload.ForwardedFrom))
                return receivedMessage.Payload.ForwardedFrom;

            return receivedMessage.FromWalletId;
        }

        private bool TryCreateMessage(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage, string senderWalletId, out ChatMessage newMessage)
        {
            newMessage = default(ChatMessage);

            if (IsUserBlockedAndMessagesHidden(senderWalletId)) return false;

            if (isChatMessageRateLimiterEnabled && !messageRateLimiter!.TryAllow(senderWalletId)) return false;

            newMessage = messageFactory.CreateChatMessage(senderWalletId, false, receivedMessage.Payload.Message, null, receivedMessage.Payload.Timestamp);

            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[ChatMessageBus] RECEIVED message: protoTimestamp={receivedMessage.Payload.Timestamp} messageId={newMessage.MessageId} from={senderWalletId}");

            return true;
        }

        private bool IsUserBlockedAndMessagesHidden(string walletAddress) =>
            userBlockingCache.HideChatMessages && userBlockingCache.UserIsBlocked(walletAddress);

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
            
            string msgId = ChatUtils.GetId(identityCache.Identity?.Address ?? "", timestamp);
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[ChatMessageBus] SENT message: timestamp={timestamp} messageId={msgId}");

            chat.SendAndDisposeAsync(cancellationTokenSource.Token, LKDataPacketKind.KindReliable).Forget();
        }
    }
}
