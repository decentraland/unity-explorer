using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatServices.ChatContextService;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
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
        private readonly CurrentChannelService currentChannelService;
        private readonly ChatContextMenuService contextMenuService;
        private readonly GetMessageHistoryCommand getMessageHistoryCommand;
        private readonly CreateMessageViewModelCommand createMessageViewModelCommand;
        private readonly MarkMessagesAsReadCommand markMessagesAsReadCommand;
        private readonly ChatScrollToBottomPresenter scrollToBottomPresenter;
        private readonly EventSubscriptionScope scope = new ();
        private CancellationTokenSource loadChannelCts = new ();

        private readonly List<ChatMessageViewModel> viewModels = new (500);

        private readonly ChatMessageViewModel separatorViewModel;

        // The index of the separator becomes fixed when
        // the new message arrives and the scroll is not at the bottom
        private int separatorFixedIndexFromBottom = -1;

        private int? messageCountWhenSeparatorViewed;

        // Ideally it should be a state,
        // especially if the state machine grows further
        private bool isFocused;

        public ChatMessageFeedPresenter(ChatMessageFeedView view,
            IEventBus eventBus,
            IChatHistory chatHistory,
            CurrentChannelService currentChannelService,
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

            scrollToBottomPresenter = new ChatScrollToBottomPresenter(view.ChatScrollToBottomView,
                currentChannelService);

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

        private bool TryAddNewMessagesSeparatorAfterPendingMessages(int previousSeparatorIndex)
        {
            if (previousSeparatorIndex == -1)
            {
                ChatChannel currentChannel = currentChannelService.CurrentChannel!;
                int unreadMessagesCount = currentChannel.Messages.Count - currentChannel.ReadMessages;

                if (unreadMessagesCount > 0)
                {
                    separatorFixedIndexFromBottom = unreadMessagesCount; // After the read message from the bottom
                    viewModels.Insert(separatorFixedIndexFromBottom, separatorViewModel);
                }

                // Otherwise separatorFixedIndexFromBottom remains -1

                return true;
            }

            return false;
        }

        private void RemoveNewMessagesSeparator()
        {
            // Remove separator from the current position
            if (separatorIsVisible)
                viewModels.RemoveAt(separatorFixedIndexFromBottom);

            separatorFixedIndexFromBottom = -1;
        }

        private void OnMessageAddedToChatHistory(ChatChannel destinationChannel, ChatMessage addedMessage, int index)
        {
            if (currentChannelService.CurrentChannel != destinationChannel)
                return;

            // 1. Capture the state BEFORE making any changes
            bool wasAtBottom = view.IsAtBottom();

            bool isSentByOwnUser = addedMessage is { IsSystemMessage: false, IsSentByOwnUser: true };

            // 2. Delegate the event to the specialized presenter. It will handle all the logic.
            scrollToBottomPresenter.OnMessageReceived(isSentByOwnUser, wasAtBottom);

            // 3. Perform the actions for the message feed itself (no button logic here)
            ChatMessageViewModel newMessageViewModel = createMessageViewModelCommand.Execute(addedMessage);

            int previousNewMessagesSeparatorIndex = separatorFixedIndexFromBottom;
            RemoveNewMessagesSeparator();

            newMessageViewModel.PendingToAnimate = true;
            viewModels.Insert(index, newMessageViewModel);

            // Handle separator logic (this is unrelated to the button)
            bool separatorAdded = false;
            if (!wasAtBottom)
            {
                if (messageCountWhenSeparatorViewed.HasValue)
                    markMessagesAsReadCommand.Execute(currentChannelService.CurrentChannel!, messageCountWhenSeparatorViewed.Value);
                separatorAdded = TryAddNewMessagesSeparatorAfterPendingMessages(previousNewMessagesSeparatorIndex);
            }

            if (!separatorAdded)
            {
                // Increment the separator index and move it to the new position
                if (previousNewMessagesSeparatorIndex > -1)
                {
                    separatorFixedIndexFromBottom = previousNewMessagesSeparatorIndex + 1;
                    viewModels.Insert(separatorFixedIndexFromBottom, separatorViewModel);
                }
            }

            messageCountWhenSeparatorViewed = null;

            // 4. Update the view and auto-scroll if necessary
            view.ReconstructScrollView(false);

            if (isSentByOwnUser || wasAtBottom)
            {
                markMessagesAsReadCommand.Execute(currentChannelService.CurrentChannel!);
                view.ShowLastMessage();
            }

            // 5. Handle the fade-out for unfocused chat
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

            string walletAddress = currentChannelService.CurrentChannel!.ChannelType == ChatChannel.ChatChannelType.USER ? currentChannelId.Id : "fake_wallet_id";

            chatHistory.AddMessage(currentChannelId, currentChannelService.CurrentChannel!.ChannelType, new ChatMessage("some message", "FakeName",
                walletAddress,
                false, "#1234"));
        }

        private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
        {
            scrollToBottomPresenter.OnChannelChanged();
            UpdateChannelMessages();
        }

        private void UpdateChannelMessages()
        {
            loadChannelCts = loadChannelCts.SafeRestart();

            LoadChannelHistory(loadChannelCts.Token).Forget();

            async UniTaskVoid LoadChannelHistory(CancellationToken ct)
            {
                try
                {
                    await getMessageHistoryCommand.ExecuteAsync(viewModels, currentChannelService.CurrentChannelId, ct);

                    RemoveNewMessagesSeparator();
                    TryAddNewMessagesSeparatorAfterPendingMessages(-1);

                    view.SetUserConnectivityProvider(currentChannelService.UserStateService!.OnlineParticipants);

                    view.ReconstructScrollView(true);
                    ScrollToNewMessagesSeparator();

                    var currentChannel = currentChannelService.CurrentChannel!;
                    int unreadCount = currentChannel.Messages.Count - currentChannel.ReadMessages;

                    if (unreadCount > 0 && !view.IsAtBottom())
                    {
                        view.SetScrollToBottomButtonVisibility(true, unreadCount, false);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.CHAT_HISTORY); }
            }
        }

        private void MarkCurrentChannelAsRead()
        {
            markMessagesAsReadCommand.Execute(currentChannelService.CurrentChannel!);
            scrollToBottomPresenter.OnScrolledToBottom();
        }

        private void OnChatHistoryCleared(ChatEvents.ChatHistoryClearedEvent evt)
        {
            scrollToBottomPresenter.OnChannelChanged();

            if (currentChannelService.CurrentChannelId.Equals(evt.ChannelId))
            {
                view.SetScrollToBottomButtonVisibility(false, 0, false);
                RemoveNewMessagesSeparator();
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
            view.OnScrollToBottomButtonClicked += OnScrollToBottomButtonClicked;

            scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
            scope.Add(eventBus.Subscribe<ChatEvents.ChatHistoryClearedEvent>(OnChatHistoryCleared));

            scrollToBottomPresenter.RequestScrollAction += OnRequestScrollAction;
            chatHistory.MessageAdded += OnMessageAddedToChatHistory;
        }

        private void Unsubscribe()
        {
            view.OnFakeMessageRequested -= OnFakeMessageRequested;
            view.OnChatContextMenuRequested -= OnChatContextMenuRequested;
            view.OnProfileContextMenuRequested -= OnProfileContextMenuRequested;
            view.OnScrolledToBottom -= MarkCurrentChannelAsRead;
            view.OnScrollPositionChanged -= OnScrollPositionChanged;
            view.OnScrollToBottomButtonClicked -= OnScrollToBottomButtonClicked;

            scope.Dispose();
            scrollToBottomPresenter.RequestScrollAction -= OnRequestScrollAction;
            chatHistory.MessageAdded -= OnMessageAddedToChatHistory;
        }

        private void OnRequestScrollAction()
        {
            view.ShowLastMessage(useSmoothScroll: true);
        }

        private void OnScrollToBottomButtonClicked()
        {
            view.ShowLastMessage(useSmoothScroll: true);
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

            RemoveNewMessagesSeparator();
            MarkCurrentChannelAsRead();
        }

        public void SetFocusState(bool isFocused, bool animate, float duration, Ease easing)
        {
            this.isFocused = isFocused;

            // Delegate logical state change to sub-presenter
            scrollToBottomPresenter.OnFocusChanged(isFocused);

            // Command its own view to update its visual state
            view.StopChatEntriesFadeout();
            if (!isFocused)
                view.StartChatEntriesFadeout();

            float targetAlpha = isFocused ? 1f : 0f;
            view.StartScrollBarFade(targetAlpha, animate ? duration : 0f, easing);
        }

        public void Dispose()
        {
            scrollToBottomPresenter.Dispose();
            loadChannelCts.SafeCancelAndDispose();
        }
    }
}
