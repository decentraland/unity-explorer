using System;
using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.ChatReactions.Presenters;
using DCL.Chat.ChatServices;
using DCL.Chat.History;
using UnityEngine;

namespace DCL.Chat.ChatMessages
{
    /// <summary>
    /// Handles user interactions with message reactions: opening the selector bar,
    /// toggling reaction pills, and showing hover tooltips.
    /// Extracted from ChatMessageFeedPresenter for single responsibility.
    /// </summary>
    public sealed class MessageReactionInteractionPresenter : IDisposable
    {
        private readonly ChatReactionsPresenter reactionsPresenter;
        private readonly ChatMessageReactionService messageReactionService;
        private readonly ReactionTooltipPresenter? tooltipPresenter;
        private readonly CurrentChannelService currentChannelService;

        private string? pendingReactionMessageId;
        private ChatEntryView? pendingReactionChatEntry;

        internal MessageReactionInteractionPresenter(
            ChatReactionsPresenter reactionsPresenter,
            ChatMessageReactionService messageReactionService,
            ReactionTooltipPresenter? tooltipPresenter,
            CurrentChannelService currentChannelService)
        {
            this.reactionsPresenter = reactionsPresenter;
            this.messageReactionService = messageReactionService;
            this.tooltipPresenter = tooltipPresenter;
            this.currentChannelService = currentChannelService;
        }

        public void OnReactionButtonClicked(string messageId, ChatEntryView chatEntryView)
        {
            if (pendingReactionMessageId == messageId)
            {
                reactionsPresenter.CloseForMessage();
                return;
            }

            ClearPendingState();
            ActivatePendingReaction(messageId, chatEntryView);

            var anchor = (RectTransform)chatEntryView.messageBubbleElement.reactionButton!.transform;
            reactionsPresenter.ShowForMessage(
                anchor,
                chatEntryView.IsSentByOwnUser,
                OnMessageReactionSelected,
                ClearPendingState);
        }

        private void OnMessageReactionSelected(int atlasIndex)
        {
            if (pendingReactionMessageId != null)
                messageReactionService.ToggleReaction(pendingReactionMessageId, atlasIndex);
        }

        private void ActivatePendingReaction(string messageId, ChatEntryView chatEntryView)
        {
            pendingReactionMessageId = messageId;
            pendingReactionChatEntry = chatEntryView;
            chatEntryView.messageBubbleElement.SetPopupOpen(true);
            chatEntryView.messageBubbleElement.reactionButtonHoverView?.SetClicked(true);
        }

        public void OnReactionPillClicked(string messageId, int emojiIndex)
        {
            HideTooltip();
            messageReactionService.ToggleReaction(messageId, emojiIndex);
        }

        public void OnReactionHoverEnter(int emojiIndex, RectTransform pillRect, string messageId)
        {
            if (tooltipPresenter == null) return;

            ReactionSet? reactions = currentChannelService.CurrentChannel?.GetReactions(messageId);
            tooltipPresenter.ShowForReaction(reactions, emojiIndex, pillRect, messageId);
        }

        public void OnReactionHoverExit(int emojiIndex)
        {
            HideTooltip();
        }

        public void HideTooltip()
        {
            tooltipPresenter?.Hide();
        }

        /// <summary>
        /// Closes any open message reaction bar and resets pending UI state.
        /// Call when the feed is unsubscribing or switching channels.
        /// </summary>
        public void Deactivate()
        {
            reactionsPresenter.CloseForMessage();
            ClearPendingState();
            HideTooltip();
        }

        public void Dispose()
        {
            tooltipPresenter?.Dispose();
        }

        private void ClearPendingState()
        {
            if (pendingReactionMessageId == null) return;

            pendingReactionChatEntry?.messageBubbleElement.reactionButtonHoverView?.SetClicked(false);
            pendingReactionChatEntry?.messageBubbleElement.SetPopupOpen(false);
            pendingReactionChatEntry = null;
            pendingReactionMessageId = null;
        }
    }
}
