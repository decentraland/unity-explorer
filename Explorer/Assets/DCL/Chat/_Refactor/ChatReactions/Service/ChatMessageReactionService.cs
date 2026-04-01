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
    /// Sends the original messageId over the wire; the receiver resolves via direct dictionary lookup.
    /// </summary>
    public sealed class ChatMessageReactionService : IRemoteReactionTarget, IDisposable
    {
        /// <summary>
        /// Fired when a reaction is added or removed and should be persisted.
        /// Parameters: channelId, messageId (local), emojiIndex, walletAddress, isRemoval.
        /// </summary>
        public event Action<ChatChannel.ChannelId, string, int, string, bool>? ReactionPersistenceRequested;

        /// <summary>
        /// Fired when the local user adds a reaction to a message (not on removal, not on remote receive).
        /// Parameters: emojiIndex, isParticipation (true if others already reacted with this emoji).
        /// </summary>
        public event Action<int, bool>? UserReactedToMessage;

        private readonly IReactionMessageBus reactionBus;
        private readonly IChatHistory chatHistory;
        private readonly IWeb3IdentityCache identityCache;

        private readonly Dictionary<string, ChatChannel.ChannelId> messageIdToChannel = new ();
        private readonly Dictionary<ChatChannel.ChannelId, HashSet<string>> channelToMessageIds = new ();

        public ChatMessageReactionService(
            IReactionMessageBus reactionBus,
            IChatHistory chatHistory,
            IWeb3IdentityCache identityCache)
        {
            this.reactionBus = reactionBus;
            this.chatHistory = chatHistory;
            this.identityCache = identityCache;

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
                SendReactionToNetwork(emojiIndex, messageId, channel.ChannelType, channelId, isRemoval: true);
            }
            else
            {
                bool othersReacted = reactions != null && reactions.GetReactors(emojiIndex) is { Count: > 0 };
                channel.AddReaction(messageId, emojiIndex, ownWallet);
                ReactionPersistenceRequested?.Invoke(channelId, messageId, emojiIndex, ownWallet, false);
                SendReactionToNetwork(emojiIndex, messageId, channel.ChannelType, channelId);
                UserReactedToMessage?.Invoke(emojiIndex, othersReacted);
            }
        }

        private void SendReactionToNetwork(int emojiIndex, string messageId,
            ChatChannel.ChatChannelType channelType, ChatChannel.ChannelId channelId, bool isRemoval = false)
        {
            int wireEmojiIndex = ReactionWireEncoding.Encode(emojiIndex, isRemoval);
            var routing = new ReactionChannelRouting(channelType, channelId.Id);
            reactionBus.SendMessageReaction(wireEmojiIndex, messageId, routing);
        }

        /// <summary>
        /// Registers all messages in a channel so their IDs can be resolved for reactions.
        /// Must be called after loading history (e.g., FillChannel) since that path
        /// does not fire MessageAdded.
        /// </summary>
        public void RegisterChannelMessages(ChatChannel channel)
        {
            ChatChannel.ChannelId channelId = channel.Id;

            if (!channelToMessageIds.TryGetValue(channelId, out HashSet<string>? idSet))
            {
                idSet = new HashSet<string>();
                channelToMessageIds[channelId] = idSet;
            }

            for (int i = 0; i < channel.Messages.Count; i++)
            {
                ChatMessage msg = channel.Messages[i];

                if (!string.IsNullOrEmpty(msg.MessageId))
                {
                    messageIdToChannel[msg.MessageId] = channelId;
                    idSet.Add(msg.MessageId);
                }
            }
        }

        public void Dispose()
        {
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
            if (!channelToMessageIds.TryGetValue(channelId, out HashSet<string>? idSet))
                return;

            ReportHub.Log(ReportCategory.CHAT_MESSAGES,
                $"[ChatMessageReactionService] Purging {idSet.Count} message(s) for channel {channelId.Id}");

            foreach (string messageId in idSet)
                messageIdToChannel.Remove(messageId);

            channelToMessageIds.Remove(channelId);
        }

        /// <summary>
        /// Handles an incoming remote message reaction routed by <see cref="ReactionRouter"/>.
        /// </summary>
        public void HandleRemoteReaction(ReactionReceivedArgs args)
        {
            if (!TryResolveIncomingReaction(args.MessageId, out string localMessageId, out ChatChannel channel))
            {
                ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES,
                    $"[ChatMessageReactionService] Resolve failed: wireKey={args.MessageId} sender={args.WalletId} emoji={args.EmojiIndex} registered={messageIdToChannel.Count}");
                return;
            }

            if (args.IsRemoval)
            {
                channel.RemoveReaction(localMessageId, args.EmojiIndex, args.WalletId);
                ReactionPersistenceRequested?.Invoke(channel.Id, localMessageId, args.EmojiIndex, args.WalletId, true);
            }
            else
            {
                channel.AddReaction(localMessageId, args.EmojiIndex, args.WalletId);
                ReactionPersistenceRequested?.Invoke(channel.Id, localMessageId, args.EmojiIndex, args.WalletId, false);
            }
        }

        private bool TryResolveIncomingReaction(string wireKey,
            out string localMessageId, out ChatChannel channel)
        {
            localMessageId = wireKey;
            channel = null!;

            return messageIdToChannel.TryGetValue(wireKey, out ChatChannel.ChannelId channelId)
                   && chatHistory.Channels.TryGetValue(channelId, out channel!);
        }

        private void OnMessageAdded(ChatChannel channel, ChatMessage message, int index)
        {
            if (string.IsNullOrEmpty(message.MessageId))
                return;

            messageIdToChannel[message.MessageId] = channel.Id;

            if (!channelToMessageIds.TryGetValue(channel.Id, out HashSet<string>? idSet))
            {
                idSet = new HashSet<string>();
                channelToMessageIds[channel.Id] = idSet;
            }

            idSet.Add(message.MessageId);
        }
    }
}
