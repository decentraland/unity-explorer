using Cysharp.Threading.Tasks;
using DCL.Chat.ChatUseCases;
using DCL.Chat.ChatViewModels;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.Services;
using DCL.Diagnostics;
using DCL.UI;
using DCL.Web3;
using DG.Tweening;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Chat.ChatMessages
{
    public class ChatMessageFeedPresenter : IndependentMVCState, IDisposable
    {
        private readonly ChatMessageFeedView view;
        private readonly IEventBus eventBus;
        private readonly IChatHistory chatHistory;
        private readonly ICurrentChannelService currentChannelService;
        private readonly ChatContextMenuService contextMenuService;
        private readonly GetMessageHistoryCommand getMessageHistoryCommand;
        private readonly CreateMessageViewModelCommand createMessageViewModelCommand;
        private readonly MarkMessagesAsReadCommand markMessagesAsReadCommand;

        private readonly EventSubscriptionScope scope = new ();
        private CancellationTokenSource loadChannelCts = new ();

        private readonly List<ChatMessageViewModel> viewModels = new (500);

        private readonly ChatMessageViewModel separatorViewModel;

        // The index of the separator becomes fixed when the new message arrives and the scroll is not at the bottom
        private int separatorFixedIndexFromBottom = -1;

        private int? messageCountWhenSeparatorViewed;

        // Ideally it should be a state, especially if the state machine grows further
        private bool isFocused;

        public ChatMessageFeedPresenter(ChatMessageFeedView view,
            IEventBus eventBus,
            IChatHistory chatHistory,
            ICurrentChannelService currentChannelService,
            ChatContextMenuService contextMenuService,
            GetMessageHistoryCommand getMessageHistoryCommand,
            CreateMessageViewModelCommand createMessageViewModelCommand,
            MarkMessagesAsReadCommand markMessagesAsReadCommand)
        {
            this.view = view;
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.currentChannelService = currentChannelService;
            this.contextMenuService = contextMenuService;
            this.getMessageHistoryCommand = getMessageHistoryCommand;
            this.createMessageViewModelCommand = createMessageViewModelCommand;
            this.markMessagesAsReadCommand = markMessagesAsReadCommand;

            separatorViewModel = createMessageViewModelCommand.ExecuteForSeparator();

            view.Initialize(viewModels);
        }

        private bool separatorIsVisible => separatorFixedIndexFromBottom > -1;

        private void OnScrollPositionChanged(Vector2 _)
        {
            if (!separatorIsVisible)
                return;

            if (!view.IsItemVisible(separatorFixedIndexFromBottom))
                return;

            messageCountWhenSeparatorViewed = currentChannelService.CurrentChannel!.Messages.Count;
        }

        private bool TryAddNewMessagesSeparatorAfterPendingMessages()
        {
            // If the separator is already fixed, it remains in the same position
            // Otherwise, calculate the reversed index based on the pending messages count
            if (!separatorIsVisible)
            {
                ChatChannel currentChannel = currentChannelService.CurrentChannel!;
                int unreadMessagesCount = currentChannel.Messages.Count - currentChannel.ReadMessages;
                separatorFixedIndexFromBottom = unreadMessagesCount > 0 ? unreadMessagesCount : -1; // After the read message from the bottom
                return true;
            }

            return false;
        }

        private void IncrementSeparatorIndex()
        {
            if (separatorIsVisible)
            {
                separatorFixedIndexFromBottom++;
                viewModels.Insert(separatorFixedIndexFromBottom, separatorViewModel);
            }
        }

        /// <summary>
        ///     Entirely removed when:
        ///     The channel has changed
        ///     The view has been minimized
        /// </summary>
        private void RemoveNewMessagesSeparator(bool unfix)
        {
            // Remove separator from the current position
            if (separatorIsVisible)
                viewModels.RemoveAt(separatorFixedIndexFromBottom);

            if (unfix)
                separatorFixedIndexFromBottom = -1;
        }

        private void OnMessageAddedToChatHistory(ChatChannel destinationChannel, ChatMessage addedMessage, int index)
        {
            // Bubbles logic should be separated from the presenter

            if (currentChannelService.CurrentChannel != destinationChannel)
                return;

            bool isSentByOwnUser = addedMessage is { IsSystemMessage: false, IsSentByOwnUser: true };

            ChatMessageViewModel newMessageViewModel = createMessageViewModelCommand.Execute(addedMessage);

            RemoveNewMessagesSeparator(false);

            newMessageViewModel.PendingToAnimate = true;
            viewModels.Insert(index, newMessageViewModel);

            if (isSentByOwnUser)
            {
                MarkCurrentChannelAsRead();

                IncrementSeparatorIndex();

                view.ReconstructScrollView(false);
                view.ShowLastMessage();
            }
            else
            {
                var separatorAdded = false;

                if (view.IsAtBottom())
                    MarkCurrentChannelAsRead();
                else
                {
                    if (messageCountWhenSeparatorViewed.HasValue)
                        markMessagesAsReadCommand.Execute(currentChannelService.CurrentChannel!, messageCountWhenSeparatorViewed.Value);

                    separatorAdded = TryAddNewMessagesSeparatorAfterPendingMessages();
                }

                if (!separatorAdded)
                    IncrementSeparatorIndex();

                messageCountWhenSeparatorViewed = null;
                view.ReconstructScrollView(false);
            }

            if (!isFocused)
                view.RestartChatEntriesFadeout();
        }

        private void ScrollToNewMessagesSeparator()
        {
            if (separatorIsVisible)
                view.ShowItem(separatorFixedIndexFromBottom);
        }

        private void OnProfileContextMenuRequested(string userId, Vector2 position)
        {
            var request = new UserProfileMenuRequest
            {
                WalletAddress = new Web3Address(userId), Position = position, AnchorPoint = MenuAnchorPoint.TOP_RIGHT, Offset = Vector2.zero,
            };

            contextMenuService.ShowUserProfileMenuAsync(request).Forget();
        }

        private void OnChatContextMenuRequested(string message, ChatEntryView? chatEntry)
        {
            var request = new ChatEntryMenuPopupData(chatEntry.messageBubbleElement.PopupPosition,
                message, chatEntry.messageBubbleElement.HideOptionsButton);

            contextMenuService.ShowChatOptionsAsync(request).Forget();
        }

        private void OnFakeMessageRequested()
        {
            ChatChannel.ChannelId currentChannelId = currentChannelService.CurrentChannelId;

            chatHistory.AddMessage(currentChannelId, currentChannelService.CurrentChannelType, new ChatMessage("some message", "validated name",
                currentChannelId.Id,
                true, "sds"));
        }

        private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
        {
            UpdateChannelMessages();
        }

        private void UpdateChannelMessages()
        {
            loadChannelCts = loadChannelCts.SafeRestart();

            RemoveNewMessagesSeparator(true);

            LoadChannelHistory(loadChannelCts.Token).Forget();

            async UniTaskVoid LoadChannelHistory(CancellationToken ct)
            {
                try
                {
                    await getMessageHistoryCommand.ExecuteAsync(viewModels, currentChannelService.CurrentChannelId, ct);
                    TryAddNewMessagesSeparatorAfterPendingMessages();
                    view.ReconstructScrollView(true);
                    ScrollToNewMessagesSeparator();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.CHAT_HISTORY); }
            }
        }

        private void MarkCurrentChannelAsRead()
        {
            markMessagesAsReadCommand.Execute(currentChannelService.CurrentChannel!);
        }

        private void OnChatHistoryCleared(ChatEvents.ChatHistoryClearedEvent evt)
        {
            if (currentChannelService.CurrentChannelId.Equals(evt.ChannelId))
            {
                RemoveNewMessagesSeparator(true);
                viewModels.ForEach(ChatMessageViewModel.RELEASE);
                viewModels.Clear();
                view.Clear();
            }
        }

        private void Subscribe()
        {
            view.OnFakeMessageRequested += OnFakeMessageRequested;
            view.OnChatContextMenuRequested += OnChatContextMenuRequested;
            view.OnProfileContextMenuRequested += OnProfileContextMenuRequested;
            view.OnScrolledToBottom += MarkCurrentChannelAsRead;
            view.OnScrollPositionChanged += OnScrollPositionChanged;

            scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
            scope.Add(eventBus.Subscribe<ChatEvents.ChatHistoryClearedEvent>(OnChatHistoryCleared));

            chatHistory.MessageAdded += OnMessageAddedToChatHistory;
        }

        private void Unsubscribe()
        {
            view.OnFakeMessageRequested -= OnFakeMessageRequested;
            view.OnChatContextMenuRequested -= OnChatContextMenuRequested;
            view.OnProfileContextMenuRequested -= OnProfileContextMenuRequested;
            view.OnScrolledToBottom -= MarkCurrentChannelAsRead;
            view.OnScrollPositionChanged -= OnScrollPositionChanged;

            scope.Dispose();
            chatHistory.MessageAdded -= OnMessageAddedToChatHistory;
        }

        protected override void Activate(ControllerNoData input)
        {
            view.Show();
            Subscribe();
            UpdateChannelMessages();
        }

        protected override void Deactivate()
        {
            view.Hide();
            Unsubscribe();

            // When the view is minimized the current channel is marked as read and the separator is removed
            RemoveNewMessagesSeparator(true);
            MarkCurrentChannelAsRead();
        }

        public void SetFocusState(bool isFocused, bool animate, float duration, Ease easing)
        {
            this.isFocused = isFocused;

            view.StopChatEntriesFadeout();

            // When the view becomes unfocused, start the timer to fade the chat entries out
            if (!isFocused)
                view.StartChatEntriesFadeout();

            float scrollBarTargetAlpha = isFocused ? 1f : 0f;
            view.StartScrollBarFade(scrollBarTargetAlpha, animate ? duration : 0f, easing);
        }

        public void Dispose()
        {
            loadChannelCts.SafeCancelAndDispose();
        }
    }
}
