using System;
using System.Collections.Generic;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Web3.Identities;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Bridges <see cref="IReactionMessageBus"/> and <see cref="ChatChannel"/> for message reactions.
    /// Routes incoming reactions to the correct channel and exposes toggle logic for outgoing reactions.
    /// Uses stable keys (rounded timestamps) for cross-client matching since relay servers may alter timestamps.
    /// </summary>
    public sealed class ChatMessageReactionService : IDisposable
    {
        /// <summary>
        /// Fired when a reaction is added or removed and should be persisted.
        /// Parameters: channelId, messageId (local), emojiIndex, walletAddress, isRemoval.
        /// </summary>
        public event Action<ChatChannel.ChannelId, string, int, string, bool>? ReactionPersistenceRequested;

        private readonly IReactionMessageBus reactionBus;
        private readonly IChatHistory chatHistory;
        private readonly IWeb3IdentityCache identityCache;

        // Reused during channel purge to avoid allocating a new list each time.
        private readonly List<string> purgeBuffer = new ();

        // Local messageId → channelId (for outgoing reactions from UI clicks).
        private readonly Dictionary<string, ChatChannel.ChannelId> messageIdToChannel = new ();

        // Stable key → local messageId (for incoming reactions from the network).
        private readonly Dictionary<string, string> stableKeyToLocalId = new ();

        // Local messageId → stable key (for outgoing reactions to the network).
        private readonly Dictionary<string, string> localIdToStableKey = new ();

        public ChatMessageReactionService(
            IReactionMessageBus reactionBus,
            IChatHistory chatHistory,
            IWeb3IdentityCache identityCache)
        {
            this.reactionBus = reactionBus;
            this.chatHistory = chatHistory;
            this.identityCache = identityCache;

            reactionBus.ReactionReceived += OnReactionReceived;
            chatHistory.MessageAdded += OnMessageAdded;
            chatHistory.ChannelCleared += OnChannelCleared;
            chatHistory.ChannelRemoved += OnChannelRemoved;
        }

        /// <summary>
        /// Toggles a reaction on a message. If the user already reacted with this emoji, removes locally.
        /// Otherwise, adds locally and sends over the network.
        /// </summary>
        public void ToggleReaction(string messageId, int emojiIndex)
        {
            string ownWallet = identityCache.Identity?.Address ?? string.Empty;

            if (string.IsNullOrEmpty(ownWallet))
            {
                ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, "[ChatMessageReactionService] Cannot toggle reaction: no wallet address");
                return;
            }

            if (!messageIdToChannel.TryGetValue(messageId, out ChatChannel.ChannelId channelId))
            {
                ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, $"[ChatMessageReactionService] Unknown messageId: {messageId}");
                return;
            }

            if (!chatHistory.Channels.TryGetValue(channelId, out ChatChannel? channel))
            {
                ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, $"[ChatMessageReactionService] ToggleReaction — channel not found in history: {channelId.Id}");
                return;
            }

            ReactionSet? reactions = channel.GetReactions(messageId);
            bool hasReacted = reactions != null && reactions.HasReacted(emojiIndex, ownWallet);

            if (hasReacted)
            {
                channel.RemoveReaction(messageId, emojiIndex, ownWallet);
                ReactionPersistenceRequested?.Invoke(channelId, messageId, emojiIndex, ownWallet, true);
            }
            else
            {
                channel.AddReaction(messageId, emojiIndex, ownWallet);
                ReactionPersistenceRequested?.Invoke(channelId, messageId, emojiIndex, ownWallet, false);
                SendReactionToNetwork(emojiIndex, messageId, channel.ChannelType, channelId);
            }
        }

        private void SendReactionToNetwork(int emojiIndex, string messageId,
            ChatChannel.ChatChannelType channelType, ChatChannel.ChannelId channelId)
        {
            string stableKey = localIdToStableKey.TryGetValue(messageId, out string? key)
                ? key
                : messageId;

            var routing = new ReactionChannelRouting(channelType, channelId.Id);
            reactionBus.SendMessageReaction(emojiIndex, stableKey, routing);
        }

        /// <summary>
        /// Registers all messages in a channel so their IDs can be resolved for reactions.
        /// Must be called after loading history (e.g., FillChannel) since that path
        /// does not fire MessageAdded.
        /// </summary>
        public void RegisterChannelMessages(ChatChannel channel)
        {
            ChatChannel.ChannelId channelId = channel.Id;

            for (int i = 0; i < channel.Messages.Count; i++)
            {
                ChatMessage msg = channel.Messages[i];

                if (!string.IsNullOrEmpty(msg.MessageId))
                    RegisterMessage(channelId, msg);
            }
        }

        public void Dispose()
        {
            reactionBus.ReactionReceived -= OnReactionReceived;
            chatHistory.MessageAdded -= OnMessageAdded;
            chatHistory.ChannelCleared -= OnChannelCleared;
            chatHistory.ChannelRemoved -= OnChannelRemoved;
        }

        private void OnChannelCleared(ChatChannel channel)
        {
            PurgeChannelEntries(channel.Id);
        }

        private void OnChannelRemoved(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType _)
        {
            PurgeChannelEntries(channelId);
        }

        private void PurgeChannelEntries(ChatChannel.ChannelId channelId)
        {
            purgeBuffer.Clear();

            foreach (var kvp in messageIdToChannel)
            {
                if (kvp.Value.Equals(channelId))
                    purgeBuffer.Add(kvp.Key);
            }

            for (int i = 0; i < purgeBuffer.Count; i++)
            {
                string msgId = purgeBuffer[i];
                messageIdToChannel.Remove(msgId);

                if (localIdToStableKey.TryGetValue(msgId, out string? stableKey))
                {
                    stableKeyToLocalId.Remove(stableKey);
                    localIdToStableKey.Remove(msgId);
                }
            }
        }

        private void OnReactionReceived(ReactionReceivedArgs args)
        {
            if (args.Type != ReactionType.Message)
                return;

            if (!TryResolveIncomingReaction(args.MessageId, args.WalletId, out string localMessageId, out ChatChannel channel))
                return;

            channel.AddReaction(localMessageId, args.EmojiIndex, args.WalletId);
            ReactionPersistenceRequested?.Invoke(channel.Id, localMessageId, args.EmojiIndex, args.WalletId, false);
        }

        /// <summary>
        /// Resolves an incoming wire key to a local messageId and channel using a three-tier fallback:
        /// 1. Stable key lookup (exact match or ±1 bucket for timestamp drift)
        /// 2. Direct messageId match
        /// 3. Channel lookup by sender wallet from the stable key
        /// </summary>
        private bool TryResolveIncomingReaction(string wireKey, string senderWallet,
            out string localMessageId, out ChatChannel channel)
        {
            localMessageId = wireKey;
            channel = null!;

            // Tier 1: stable key → local messageId
            if (!TryResolveStableKey(wireKey, out string? resolvedId))
            {
                // Tier 2: use wire key directly if it's a known messageId
                if (!messageIdToChannel.ContainsKey(wireKey))
                {
                    // Tier 3: try to find channel by wallet embedded in the key
                    if (!TryFindChannelForStableKey(wireKey, senderWallet, out _))
                        return false;
                }
            }
            else
            {
                localMessageId = resolvedId!;
            }

            // Resolve the channel for the local messageId
            if (!messageIdToChannel.TryGetValue(localMessageId, out ChatChannel.ChannelId channelId))
            {
                if (!TryFindChannelForStableKey(wireKey, senderWallet, out channelId))
                    return false;
            }

            return chatHistory.Channels.TryGetValue(channelId, out channel!);
        }

        private void OnMessageAdded(ChatChannel channel, ChatMessage message, int index)
        {
            if (!string.IsNullOrEmpty(message.MessageId))
                RegisterMessage(channel.Id, message);
        }

        private void RegisterMessage(ChatChannel.ChannelId channelId, ChatMessage message)
        {
            string messageId = message.MessageId;
            messageIdToChannel[messageId] = channelId;

            string stableKey = ChatUtils.GetStableReactionKey(
                message.SenderWalletAddress, message.SentTimestampRaw);

            stableKeyToLocalId[stableKey] = messageId;
            localIdToStableKey[messageId] = stableKey;
        }

        /// <summary>
        /// Tries to resolve a wire stable key to a local messageId. Checks adjacent buckets (±1)
        /// to absorb relay timestamp drift that crosses a floor boundary.
        /// </summary>
        private bool TryResolveStableKey(string wireKey, out string? localMessageId)
        {
            if (stableKeyToLocalId.TryGetValue(wireKey, out localMessageId))
                return true;

            int colonIdx = wireKey.LastIndexOf(':');
            if (colonIdx <= 0 || !long.TryParse(wireKey.Substring(colonIdx + 1), out long bucket))
            {
                localMessageId = null;
                return false;
            }

            string walletPrefix = wireKey.Substring(0, colonIdx + 1);

            if (stableKeyToLocalId.TryGetValue(string.Concat(walletPrefix, (bucket - 1).ToString()), out localMessageId))
                return true;

            if (stableKeyToLocalId.TryGetValue(string.Concat(walletPrefix, (bucket + 1).ToString()), out localMessageId))
                return true;

            localMessageId = null;
            return false;
        }

        private bool TryFindChannelForStableKey(string stableKey, string reactionSenderWallet, out ChatChannel.ChannelId channelId)
        {
            channelId = default;

            int colonIdx = stableKey.IndexOf(':');

            if (colonIdx > 0)
            {
                string wallet = stableKey.Substring(0, colonIdx);
                var candidate = new ChatChannel.ChannelId(wallet);

                if (chatHistory.Channels.TryGetValue(candidate, out ChatChannel? ch)
                    && ch.ChannelType == ChatChannel.ChatChannelType.USER)
                {
                    channelId = candidate;
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(reactionSenderWallet))
            {
                var candidate = new ChatChannel.ChannelId(reactionSenderWallet);

                if (chatHistory.Channels.TryGetValue(candidate, out ChatChannel? ch)
                    && ch.ChannelType == ChatChannel.ChatChannelType.USER)
                {
                    channelId = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
