using System;
using DCL.Chat.ChatReactions;
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
        private readonly Func<string, ReactionSet?> getReactions;

        private string? pendingReactionMessageId;
        private ChatEntryView? pendingReactionChatEntry;

        public MessageReactionInteractionPresenter(
            ChatReactionsPresenter reactionsPresenter,
            ChatMessageReactionService messageReactionService,
            ReactionTooltipPresenter? tooltipPresenter,
            Func<string, ReactionSet?> getReactions)
        {
            this.reactionsPresenter = reactionsPresenter;
            this.messageReactionService = messageReactionService;
            this.tooltipPresenter = tooltipPresenter;
            this.getReactions = getReactions;
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
                atlasIndex => messageReactionService.ToggleReaction(messageId, atlasIndex),
                ClearPendingState);
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
            tooltipPresenter?.Hide();
            messageReactionService.ToggleReaction(messageId, emojiIndex);
        }

        public void OnReactionHoverEnter(int emojiIndex, RectTransform pillRect, string messageId, bool isOffline)
        {
            if (tooltipPresenter == null) return;

            if (isOffline)
            {
                tooltipPresenter.ShowOfflineTooltip(pillRect);
                return;
            }

            ReactionSet? reactions = getReactions(messageId);
            tooltipPresenter.ShowForReaction(reactions, emojiIndex, pillRect, messageId);
        }

        public void OnReactionHoverExit(int emojiIndex)
        {
            tooltipPresenter?.Hide();
        }

        public void OnReactionChanged(string messageId)
        {
            tooltipPresenter?.HideIfShowingMessage(messageId);
        }

        /// <summary>
        /// Closes any open message reaction bar and resets pending UI state.
        /// Call when the feed is unsubscribing or switching channels.
        /// </summary>
        public void Deactivate()
        {
            reactionsPresenter.CloseForMessage();
            ClearPendingState();
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
