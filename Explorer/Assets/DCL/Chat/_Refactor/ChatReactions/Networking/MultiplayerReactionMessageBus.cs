using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat.MessageBus.Deduplication;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Deduplication;
using DCL.Utilities;
using DCL.Web3.Identities;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Proto;
using UnityEngine;
using Utility;

namespace DCL.Chat.ChatReactions
{
    public sealed class MultiplayerReactionMessageBus : IReactionMessageBus
    {
        private readonly IMessagePipesHub messagePipesHub;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly IWeb3IdentityCache identityCache;
        private readonly string routingUser;
        private readonly CancellationTokenSource cts = new ();
        private readonly IMessageDeduplication<float> situationalDedup = new MessageDeduplication<float>();
        private readonly IMessageDeduplication<string> chatReactionDedup = new MessageDeduplication<string>();

        public event Action<ReactionReceivedArgs>? ReactionReceived;

        public MultiplayerReactionMessageBus(
            IMessagePipesHub messagePipesHub,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            IWeb3IdentityCache identityCache,
            string routingUser)
        {
            this.messagePipesHub = messagePipesHub;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.identityCache = identityCache;
            this.routingUser = routingUser;

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

            float timestamp = overrideTimestamp > 0f ? overrideTimestamp : Time.unscaledTime;
            int sendCount = Mathf.Max(1, count);

            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] Sending situational reaction: emoji={emojiIndex} count={sendCount} ts={timestamp}");

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
            reaction.SendAndDisposeAsync(cts.Token, DataPacketKind.KindReliable).Forget();
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
            reaction.SendAndDisposeAsync(cts.Token, DataPacketKind.KindReliable).Forget();
        }

        private void OnSituationalReactionReceived(ReceivedMessage<Reaction> receivedMessage)
        {
            using (receivedMessage)
            {
                if (cts.IsCancellationRequested || IsUserBlocked(receivedMessage.FromWalletId))
                    return;

                float timestamp = receivedMessage.Payload.Timestamp != 0f
                    ? receivedMessage.Payload.Timestamp
                    : Time.unscaledTime;

                if (!situationalDedup.TryPass(receivedMessage.FromWalletId, timestamp))
                    return;

                int count = Mathf.Max(1, receivedMessage.Payload.Count);

                ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] Received situational reaction: emoji={receivedMessage.Payload.EmojiIndex} count={count} from={receivedMessage.FromWalletId}");

                ReactionReceived?.Invoke(new ReactionReceivedArgs(
                    receivedMessage.FromWalletId,
                    receivedMessage.Payload.EmojiIndex,
                    count,
                    ReactionType.Situational,
                    string.Empty));
            }
        }

        private void OnChatReactionReceived(ReceivedMessage<ChatReaction> receivedMessage)
        {
            using (receivedMessage)
            {
                string walletId = !string.IsNullOrEmpty(receivedMessage.Payload.Address)
                    ? receivedMessage.Payload.Address
                    : receivedMessage.FromWalletId;

                ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] OnChatReactionReceived raw: emoji={receivedMessage.Payload.EmojiIndex} messageId={receivedMessage.Payload.MessageId} address={receivedMessage.Payload.Address} fromWallet={receivedMessage.FromWalletId}");

                if (cts.IsCancellationRequested)
                {
                    ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, "[MultiplayerReactionBus] OnChatReactionReceived skipped — CTS cancelled");
                    return;
                }

                if (IsUserBlocked(walletId))
                {
                    ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] OnChatReactionReceived skipped — user blocked: {walletId}");
                    return;
                }

                int rawEmojiIndex = receivedMessage.Payload.EmojiIndex;
                var (emojiIndex, isRemoval) = ReactionWireEncoding.Decode(rawEmojiIndex);

                // Use raw value in dedup key so add/remove have distinct keys.
                // Evict the opposite key so toggling (add→remove→add) isn't blocked.
                string dedupKey = $"{receivedMessage.Payload.MessageId}:{rawEmojiIndex}";
                string oppositeKey = $"{receivedMessage.Payload.MessageId}:{ReactionWireEncoding.Encode(emojiIndex, !isRemoval)}";

                if (!chatReactionDedup.TryPass(walletId, dedupKey))
                {
                    ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] OnChatReactionReceived skipped — dedup: {dedupKey} from={walletId}");
                    return;
                }

                chatReactionDedup.Remove(walletId, oppositeKey);

                ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] Received chat reaction: emoji={emojiIndex} isRemoval={isRemoval} messageId={receivedMessage.Payload.MessageId} from={walletId}");

                ReactionReceived?.Invoke(new ReactionReceivedArgs(
                    walletId,
                    emojiIndex,
                    1,
                    ReactionType.Message,
                    receivedMessage.Payload.MessageId,
                    isRemoval));
            }
        }

        private bool IsUserBlocked(string userAddress) =>
            userBlockingCacheProxy.Configured && userBlockingCacheProxy.Object!.UserIsBlocked(userAddress);
    }
}
