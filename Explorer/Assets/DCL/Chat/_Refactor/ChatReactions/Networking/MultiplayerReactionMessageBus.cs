using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.History;
using DCL.Chat.MessageBus.Deduplication;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.LiveKit.Public;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Deduplication;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Proto;
using UnityEngine;
using Utility;

namespace DCL.Chat.ChatReactions.Networking
{
    public sealed class MultiplayerReactionMessageBus : IReactionMessageBus
    {
        // Absolute sanity bound for emoji indices arriving from the network.
        // The emoji panel has ~3,580 entries (358 pages × 10); 4096 is a safe power-of-two ceiling.
        // The effective bound is tightened to the actual atlas tile count via the constructor.
        private const int MAX_VALID_EMOJI_INDEX = 4096;

        // Message IDs are produced locally in one of two shapes: ChatUtils.GetId
        // ("{42-char address}:{invariant double}", ~67 characters) or a 36-character GUID for
        // system messages. 96 clears both with headroom while keeping an ID that arrives from
        // the network bounded before it is used to build anything.
        private const int MAX_MESSAGE_ID_LENGTH = 96;

        // Hard ceiling on the dedup keys either cache retains. A flood of distinct keys restarts
        // the window instead of growing it, bounding the memory a peer can make this bus hold.
        private const int MAX_DEDUP_ENTRIES = 2048;

        private readonly IMessagePipesHub messagePipesHub;
        private readonly IUserBlockingCache userBlockingCache;
        private readonly IWeb3IdentityCache identityCache;
        private readonly string routingUser;
        private readonly int maxValidEmojiIndex;
        private readonly int situationalReceiveCountCap;
        private readonly CancellationTokenSource cts = new ();
        private readonly IMessageDeduplication<float> situationalDedup = new MessageDeduplication<float>(MAX_DEDUP_ENTRIES);
        private readonly IMessageDeduplication<string> chatReactionDedup = new MessageDeduplication<string>(MAX_DEDUP_ENTRIES);
        private readonly PerSenderRateLimiter situationalRateLimiter;
        private readonly PerSenderRateLimiter chatReactionRateLimiter;

        public event Action<ReactionReceivedArgs>? ReactionReceived;

        internal MultiplayerReactionMessageBus(
            IMessagePipesHub messagePipesHub,
            IUserBlockingCache userBlockingCache,
            IWeb3IdentityCache identityCache,
            string routingUser,
            ChatReactionsConfig config,
            int maxValidEmojiIndex = MAX_VALID_EMOJI_INDEX)
        {
            this.messagePipesHub = messagePipesHub;
            this.userBlockingCache = userBlockingCache;
            this.identityCache = identityCache;
            this.routingUser = routingUser;
            this.maxValidEmojiIndex = Mathf.Clamp(maxValidEmojiIndex, 1, MAX_VALID_EMOJI_INDEX);
            situationalReceiveCountCap = config.SituationalReceiveCountCap;

            situationalRateLimiter = new PerSenderRateLimiter(
                config.SituationalReceiveRatePerSecond, config.SituationalReceiveBurst, config.MaxRateTrackedSenders);

            chatReactionRateLimiter = new PerSenderRateLimiter(
                config.ChatReactionReceiveRatePerSecond, config.ChatReactionReceiveBurst, config.MaxRateTrackedSenders);

            ReportHub.Log(ReportCategory.CHAT_MESSAGES, "[MultiplayerReactionBus] Subscribing to Reaction and ChatReaction on Island/Scene/Chat pipes");

            messagePipesHub.IslandPipe().Subscribe<Reaction>(Packet.MessageOneofCase.Reaction, OnSituationalReactionReceived);
            messagePipesHub.ScenePipe().Subscribe<Reaction>(Packet.MessageOneofCase.Reaction, OnSituationalReactionReceived);

            messagePipesHub.IslandPipe().Subscribe<ChatReaction>(Packet.MessageOneofCase.ChatReaction, OnChatReactionReceived);
            messagePipesHub.ScenePipe().Subscribe<ChatReaction>(Packet.MessageOneofCase.ChatReaction, OnChatReactionReceived);
            messagePipesHub.ChatPipe().Subscribe<ChatReaction>(Packet.MessageOneofCase.ChatReaction, OnChatReactionReceived);
        }

        public void SendSituationalReaction(int emojiIndex, int count = 1, float overrideTimestamp = 0f)
        {
            if (cts.IsCancellationRequested) return;

            float timestamp = overrideTimestamp > 0f ? overrideTimestamp : UnityEngine.Time.unscaledTime;
            int sendCount = Mathf.Max(1, count);

            SendReactionTo(emojiIndex, sendCount, timestamp, messagePipesHub.IslandPipe());
            SendReactionTo(emojiIndex, sendCount, timestamp, messagePipesHub.ScenePipe());
        }

        public void SendMessageReaction(int emojiIndex, string messageId, ReactionChannelRouting routing)
        {
            if (cts.IsCancellationRequested)
            {
                ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, "[MultiplayerReactionBus] SendMessageReaction skipped — CTS cancelled");
                return;
            }

            string address = identityCache.Identity?.Address ?? string.Empty;

            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] Sending chat reaction: emoji={emojiIndex} messageId={messageId} channelType={routing.ChannelType}");

            switch (routing.ChannelType)
            {
                case History.ChatChannel.ChatChannelType.NEARBY:
                    SendChatReactionTo(emojiIndex, messageId, address, messagePipesHub.IslandPipe());
                    SendChatReactionTo(emojiIndex, messageId, address, messagePipesHub.ScenePipe());
                    break;
                case History.ChatChannel.ChatChannelType.USER:
                    SendChatReactionTo(emojiIndex, messageId, address, messagePipesHub.ChatPipe(),
                        topic: routing.ChannelId, recipient: routing.ChannelId);
                    break;
                case History.ChatChannel.ChatChannelType.COMMUNITY:
                    SendChatReactionTo(emojiIndex, messageId, address, messagePipesHub.ChatPipe(),
                        topic: routing.ChannelId, recipient: routingUser);
                    break;
                case ChatChannel.ChatChannelType.UNDEFINED:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
        }

        private void SendReactionTo(int emojiIndex, int count, float timestamp, IMessagePipe messagePipe)
        {
            MessageWrap<Reaction> reaction = messagePipe.NewMessage<Reaction>();
            reaction.Payload.EmojiIndex = emojiIndex;
            reaction.Payload.Timestamp = timestamp;
            reaction.Payload.Count = count;
            reaction.SendAndDisposeAsync(cts.Token, LKDataPacketKind.KindReliable).Forget();
        }

        private void SendChatReactionTo(int emojiIndex, string messageId, string address,
            IMessagePipe messagePipe, string topic = "", string? recipient = null)
        {
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] SendChatReactionTo pipe={messagePipe.GetType().Name} emoji={emojiIndex} messageId={messageId} address={address} topic={topic} recipient={recipient}");

            MessageWrap<ChatReaction> reaction = messagePipe.NewMessage<ChatReaction>(topic);

            if (recipient != null)
                reaction.AddSpecialRecipient(recipient);

            reaction.Payload.EmojiIndex = emojiIndex;
            reaction.Payload.MessageId = messageId;
            reaction.Payload.Address = address;
            reaction.SendAndDisposeAsync(cts.Token, LKDataPacketKind.KindReliable).Forget();
        }

        private void OnSituationalReactionReceived(ReceivedMessage<Reaction> receivedMessage)
        {
            using (receivedMessage)
            {
                if (cts.IsCancellationRequested || IsUserBlocked(receivedMessage.FromWalletId))
                    return;

                float timestamp = receivedMessage.Payload.Timestamp != 0f
                    ? receivedMessage.Payload.Timestamp
                    : UnityEngine.Time.unscaledTime;

                if (!situationalDedup.TryPass(receivedMessage.FromWalletId, timestamp))
                    return;

                int emojiIndex = receivedMessage.Payload.EmojiIndex;

                if (emojiIndex < 0 || emojiIndex >= maxValidEmojiIndex)
                {
                    ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] Rejected situational reaction with out-of-range emoji index {emojiIndex} from={receivedMessage.FromWalletId}");
                    return;
                }

                // Silent drop: a flood that exceeds the budget must not pay for per-drop logging either.
                if (!situationalRateLimiter.TryPass(receivedMessage.FromWalletId, UnityEngine.Time.unscaledTime))
                    return;

                int count = Mathf.Clamp(receivedMessage.Payload.Count, 1, situationalReceiveCountCap);

                ReactionReceived?.Invoke(new ReactionReceivedArgs(
                    receivedMessage.FromWalletId,
                    emojiIndex,
                    count,
                    ReactionType.Situational,
                    string.Empty));
            }
        }

        private void OnChatReactionReceived(ReceivedMessage<ChatReaction> receivedMessage)
        {
            using (receivedMessage)
            {
                if (cts.IsCancellationRequested)
                {
                    ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, "[MultiplayerReactionBus] OnChatReactionReceived skipped — CTS cancelled");
                    return;
                }

                string messageId = receivedMessage.Payload.MessageId;

                // Checked before the dedup keys exist, so an ID no local message could carry
                // never reaches the dedup cache. Dropped silently: ReportHub.LogWarning is not
                // compiled out, so naming the ID here would allocate it once per packet in
                // retail builds.
                if (string.IsNullOrEmpty(messageId) || messageId.Length > MAX_MESSAGE_ID_LENGTH)
                    return;

                string walletId = ResolveSenderWalletId(receivedMessage);

                ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] OnChatReactionReceived raw: emoji={receivedMessage.Payload.EmojiIndex} messageId={messageId} fromWallet={receivedMessage.FromWalletId} resolved={walletId}");

                if (IsUserBlocked(walletId))
                {
                    ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] OnChatReactionReceived skipped — user blocked: {walletId}");
                    return;
                }

                int rawEmojiIndex = receivedMessage.Payload.EmojiIndex;
                var (emojiIndex, isRemoval) = ReactionWireEncoding.Decode(rawEmojiIndex);

                if (emojiIndex < 0 || emojiIndex >= maxValidEmojiIndex)
                {
                    ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] Rejected chat reaction with out-of-range emoji index {emojiIndex} from={walletId}");
                    return;
                }

                // Use raw value in dedup key so add/remove have distinct keys.
                // Evict the opposite key so toggling (add→remove→add) isn't blocked.
                string dedupKey = $"{messageId}:{rawEmojiIndex}";
                string oppositeKey = $"{messageId}:{ReactionWireEncoding.Encode(emojiIndex, !isRemoval)}";

                if (!chatReactionDedup.TryPass(walletId, dedupKey))
                {
                    ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] OnChatReactionReceived skipped — dedup: {dedupKey} from={walletId}");
                    return;
                }

                chatReactionDedup.Remove(walletId, oppositeKey);

                // After dedup so pipe duplicates don't consume budget.
                // Silent drop: a flood that exceeds the budget must not pay for per-drop logging either.
                if (!chatReactionRateLimiter.TryPass(walletId, UnityEngine.Time.unscaledTime))
                    return;

                ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] Received chat reaction: emoji={emojiIndex} isRemoval={isRemoval} messageId={messageId} from={walletId}");

                ReactionReceived?.Invoke(new ReactionReceivedArgs(
                    walletId,
                    emojiIndex,
                    1,
                    ReactionType.Message,
                    messageId,
                    isRemoval));
            }
        }

        private string ResolveSenderWalletId(ReceivedMessage<ChatReaction> receivedMessage)
        {
            // Community reactions relayed through the message-router arrive carrying the relay's
            // identity in FromWalletId, so the original sender can only come from the payload.
            // Reading it solely from the router, and solely when it is a canonical wallet
            // address, keeps a direct peer from asserting an arbitrary identity and keeps the
            // block, dedup and rate-limit keys bounded.
            // NOTE: Add a server-stamped ForwardedFrom to the ChatReaction protocol (like Chat
            // has) so the relay writes the verified sender identity, not the client.
            if (receivedMessage.FromWalletId != routingUser)
                return receivedMessage.FromWalletId;

            string relayedAddress = receivedMessage.Payload.Address;

            return Web3Address.IsValidWalletAddress(relayedAddress)
                ? relayedAddress
                : receivedMessage.FromWalletId;
        }

        private bool IsUserBlocked(string userAddress) =>
            userBlockingCache.UserIsBlocked(userAddress);
    }
}
