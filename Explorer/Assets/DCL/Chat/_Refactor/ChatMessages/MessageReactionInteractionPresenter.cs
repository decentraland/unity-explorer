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
        private readonly ReactionTooltipPresenter tooltipPresenter;
        private readonly ReactionLimitToastView limitToastView;
        private readonly string limitToastMessage;
        private readonly CurrentChannelService currentChannelService;

        private string? pendingReactionMessageId;
        private ChatEntryView? pendingReactionChatEntry;

        internal MessageReactionInteractionPresenter(
            ChatReactionsPresenter reactionsPresenter,
            ChatMessageReactionService messageReactionService,
            ReactionTooltipPresenter tooltipPresenter,
            CurrentChannelService currentChannelService,
            ReactionLimitToastView limitToastView,
            string limitToastMessage = "")
        {
            this.reactionsPresenter = reactionsPresenter;
            this.messageReactionService = messageReactionService;
            this.tooltipPresenter = tooltipPresenter;
            this.currentChannelService = currentChannelService;
            this.limitToastView = limitToastView;
            this.limitToastMessage = limitToastMessage;

            reactionsPresenter.MessageReactionRequested += OnMessageReactionSelected;
            reactionsPresenter.MessageBarDismissed += ClearPendingState;

            if (limitToastView != null)
                messageReactionService.ReactionLimitReached += OnReactionLimitReached;
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
                chatEntryView.IsSentByOwnUser);
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
            chatEntryView.messageBubbleElement.SetReactionPopupActive(true);
        }

        public void OnReactionPillClicked(string messageId, int emojiIndex)
        {
            HideTooltip();
            messageReactionService.ToggleReaction(messageId, emojiIndex);
        }

        public void OnReactionHoverEnter(int emojiIndex, RectTransform pillRect, string messageId)
        {
            ReactionSet? reactions = currentChannelService.CurrentChannel.GetReactions(messageId);
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
            limitToastView?.Hide();
        }

        public void Dispose()
        {
            reactionsPresenter.MessageReactionRequested -= OnMessageReactionSelected;
            reactionsPresenter.MessageBarDismissed -= ClearPendingState;

            tooltipPresenter?.Dispose();
            limitToastView?.Hide();

            if (limitToastView != null)
                messageReactionService.ReactionLimitReached -= OnReactionLimitReached;
        }

        private void OnReactionLimitReached(int max)
        {
            // pendingReactionChatEntry is always set here: the limit can only fire from
            // the selector bar (new distinct emoji), which requires OnReactionButtonClicked.
            if (limitToastView == null || pendingReactionChatEntry == null)
                return;

            var anchor = (RectTransform)pendingReactionChatEntry.messageBubbleElement.reactionButton!.transform;
            string message = string.Format(limitToastMessage, max);
            limitToastView.Show(message, anchor);
        }

        private void ClearPendingState()
        {
            if (pendingReactionMessageId == null) return;

            pendingReactionChatEntry?.messageBubbleElement.SetReactionPopupActive(false);
            pendingReactionChatEntry = null;
            pendingReactionMessageId = null;
        }
    }
}
