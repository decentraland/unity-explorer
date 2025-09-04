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
using System.Linq;
using System.Threading;
using DCL.Translation.Events;
using DCL.Translation.Service.Memory;
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
        private readonly ITranslationMemory translationMemory;
        private readonly ChatScrollToBottomPresenter scrollToBottomPresenter;
        private readonly EventSubscriptionScope scope = new ();

        private readonly List<ChatMessageViewModel> viewModels = new (500);

        private readonly ChatMessageViewModel separatorViewModel;

        private IDisposable? onChannelSelectedSubscription;

        private CancellationTokenSource loadChannelCts = new ();

        // The index of the separator becomes fixed when
        // the new message arrives and the scroll is not at the bottom
        private int separatorFixedIndexFromBottom = -1;

        private int? messageCountWhenSeparatorViewed;

        // Ideally it should be a state,
        // especially if the state machine grows further
        private bool isFocused;

        private bool separatorIsVisible => separatorFixedIndexFromBottom > -1;

        public ChatMessageFeedPresenter(ChatMessageFeedView view,
            IEventBus eventBus,
            IChatHistory chatHistory,
            CurrentChannelService currentChannelService,
            ChatContextMenuService contextMenuService,
            ITranslationMemory translationMemory,
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
            this.translationMemory = translationMemory;

            scrollToBottomPresenter = new ChatScrollToBottomPresenter(view.ChatScrollToBottomView,
                currentChannelService);

            separatorViewModel = createMessageViewModelCommand.ExecuteForSeparator();
            scope.Add(eventBus.Subscribe<ChatEvents.ChatResetEvent>(OnChatReset));
            view.Initialize(viewModels);
        }

        private void OnChatReset(ChatEvents.ChatResetEvent obj)
        {
            loadChannelCts.SafeCancelAndDispose();

            viewModels.ForEach(ChatMessageViewModel.RELEASE);

            viewModels.Clear();

            view.Clear();

            scrollToBottomPresenter.OnChannelChanged();
            separatorFixedIndexFromBottom = -1;
            messageCountWhenSeparatorViewed = null;
            isFocused = false;
        }

        public void Dispose()
        {
            scrollToBottomPresenter.Dispose();
            loadChannelCts.SafeCancelAndDispose();
        }

        private void OnScrollPositionChanged(Vector2 _)
        {
            if (!separatorIsVisible)
                return;

            if (!view.IsItemVisible(separatorFixedIndexFromBottom))
                return;

            // It is possible that the current channel is not yet set (initialization is in progress)
            if (currentChannelService.CurrentChannel == null) return;

            messageCountWhenSeparatorViewed = currentChannelService.CurrentChannel!.Messages.Count;
        }

        private void TryAddNewMessagesSeparatorAfterPendingMessages()
        {
            ChatChannel currentChannel = currentChannelService.CurrentChannel!;
            int unreadMessagesCount = currentChannel.Messages.Count - currentChannel.ReadMessages;

            if (unreadMessagesCount > 0)
            {
                separatorFixedIndexFromBottom = unreadMessagesCount; // After the read message from the bottom
                viewModels.Insert(separatorFixedIndexFromBottom, separatorViewModel);
            }
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

            // 2. Perform the actions for the message feed itself (no button logic here)
            (bool isTopMostMessage, ChatMessage? previousMessage) = GetMessageHistoryCommand.GetTopMostAndPreviousMessage(destinationChannel.Messages, index);
            ChatMessageViewModel newMessageViewModel = createMessageViewModelCommand.Execute(addedMessage, previousMessage, isTopMostMessage);

            int previousNewMessagesSeparatorIndex = separatorFixedIndexFromBottom;
            bool separatorWasVisible = previousNewMessagesSeparatorIndex > -1;
            RemoveNewMessagesSeparator();

            newMessageViewModel.PendingToAnimate = true;
            if (translationMemory.TryGet(addedMessage.MessageId, out var translation))
            {
                newMessageViewModel.TranslationState = translation.State;
                newMessageViewModel.TranslatedText = translation.TranslatedBody;
            }
            
            viewModels.Insert(index, newMessageViewModel);

            // Handle separator logic (this is unrelated to the button)
            bool qualifiedForAddingSeparator = !wasAtBottom && !isSentByOwnUser;

            if (qualifiedForAddingSeparator)
            {
                // Mark only those messages that were viewed
                if (messageCountWhenSeparatorViewed.HasValue)
                    markMessagesAsReadCommand.Execute(currentChannelService.CurrentChannel!, messageCountWhenSeparatorViewed.Value);

                TryAddNewMessagesSeparatorAfterPendingMessages();
            }
            else if (separatorWasVisible)
            {
                // If the separator was already visible increment its index to preserve its visual position
                separatorFixedIndexFromBottom = previousNewMessagesSeparatorIndex + 1;
                viewModels.Insert(separatorFixedIndexFromBottom, separatorViewModel);
            }

            messageCountWhenSeparatorViewed = null;

            // 3. Update the view and auto-scroll if necessary
            view.ReconstructScrollView(false);

            if (!qualifiedForAddingSeparator)
            {
                markMessagesAsReadCommand.Execute(currentChannelService.CurrentChannel!);
                view.ShowLastMessage();
            }

            // 4. Delegate the event to the specialized presenter. It will handle all the logic. It should be done after messages are marked as read
            scrollToBottomPresenter.OnMessageReceived(isSentByOwnUser, wasAtBottom);

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

        private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
        {
            scrollToBottomPresenter.OnChannelChanged();
            UpdateChannelMessages();
        }

        private void UpdateViewModelAndRefreshView(string messageId)
        {
            // Find the ViewModel in our current list
            var viewModel = viewModels.FirstOrDefault(vm => vm.Message.MessageId == messageId);
            if (viewModel == null) return;

            // Get the latest state from the memory service
            if (translationMemory.TryGet(messageId, out var translation))
            {
                viewModel.TranslationState = translation.State;
                viewModel.TranslatedText = translation.TranslatedBody;
                // You could also add `viewModel.TranslationError = translation.Error;`
            }

            // Find the index of the ViewModel in the list
            int itemIndex = viewModels.IndexOf(viewModel);
            if (itemIndex == -1) return;

            // Tell the view to refresh this specific item
            view.RefreshItem(itemIndex);
        }

        private void OnMessageTranslationRequested(TranslationEvents.MessageTranslationRequested evt)
        {
            UpdateViewModelAndRefreshView(evt.MessageId);
        }

        private void OnMessageTranslated(TranslationEvents.MessageTranslated evt)
        {
            UpdateViewModelAndRefreshView(evt.MessageId);
        }

        private void OnMessageTranslationFailed(TranslationEvents.MessageTranslationFailed evt)
        {
            UpdateViewModelAndRefreshView(evt.MessageId);
        }

        private void OnMessageTranslationReverted(TranslationEvents.MessageTranslationReverted evt)
        {
            UpdateViewModelAndRefreshView(evt.MessageId);
        }
        
        private void UpdateChannelMessages()
        {
            loadChannelCts = loadChannelCts.SafeRestart();

            RemoveNewMessagesSeparator();

            // When the history the state is not final so the events should be ignored
            Unsubscribe();

            LoadChannelHistoryAsync(loadChannelCts.Token).Forget();

            async UniTaskVoid LoadChannelHistoryAsync(CancellationToken ct)
            {
                try
                {
                    await getMessageHistoryCommand.ExecuteAsync(viewModels, currentChannelService.CurrentChannelId, ct);
                    TryAddNewMessagesSeparatorAfterPendingMessages();

                    Subscribe();

                    view.SetUserConnectivityProvider(currentChannelService.UserStateService!.OnlineParticipants);

                    view.ReconstructScrollView(true);
                    ScrollToNewMessagesSeparator();

                    ChatChannel currentChannel = currentChannelService.CurrentChannel!;
                    int unreadCount = currentChannel.Messages.Count - currentChannel.ReadMessages;

                    if (unreadCount > 0 && !view.IsAtBottom()) { view.SetScrollToBottomButtonVisibility(true, unreadCount, false); }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.CHAT_HISTORY); }
            }
        }

        private void MarkCurrentChannelAsRead()
        {
            if (currentChannelService.CurrentChannel == null) return;

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
            view.OnChatContextMenuRequested += OnChatContextMenuRequested;
            view.OnProfileContextMenuRequested += OnProfileContextMenuRequested;
            view.OnScrolledToBottom += MarkCurrentChannelAsRead;
            view.OnScrollPositionChanged += OnScrollPositionChanged;
            view.OnScrollToBottomButtonClicked += OnScrollToBottomButtonClicked;

            scope.Add(eventBus.Subscribe<ChatEvents.ChatHistoryClearedEvent>(OnChatHistoryCleared));
            scope.Add(eventBus.Subscribe<ChatEvents.ChannelUsersStatusUpdated>(OnChannelUsersUpdated));
            scope.Add(eventBus.Subscribe<ChatEvents.UserStatusUpdatedEvent>(OnUserStatusUpdated));
            scope.Add(eventBus.Subscribe<TranslationEvents.MessageTranslationRequested>(OnMessageTranslationRequested));
            scope.Add(eventBus.Subscribe<TranslationEvents.MessageTranslated>(OnMessageTranslated));
            scope.Add(eventBus.Subscribe<TranslationEvents.MessageTranslationFailed>(OnMessageTranslationFailed));
            scope.Add(eventBus.Subscribe<TranslationEvents.MessageTranslationReverted>(OnMessageTranslationReverted));

            scrollToBottomPresenter.RequestScrollAction += OnRequestScrollAction;
            chatHistory.MessageAdded += OnMessageAddedToChatHistory;
        }

        private void Unsubscribe()
        {
            view.OnChatContextMenuRequested -= OnChatContextMenuRequested;
            view.OnProfileContextMenuRequested -= OnProfileContextMenuRequested;
            view.OnScrolledToBottom -= MarkCurrentChannelAsRead;
            view.OnScrollPositionChanged -= OnScrollPositionChanged;
            view.OnScrollToBottomButtonClicked -= OnScrollToBottomButtonClicked;

            scope.Dispose();
            scrollToBottomPresenter.RequestScrollAction -= OnRequestScrollAction;
            chatHistory.MessageAdded -= OnMessageAddedToChatHistory;
        }

        private void OnUserStatusUpdated(ChatEvents.UserStatusUpdatedEvent upd)
        {
            if (upd.ChannelId.Equals(currentChannelService.CurrentChannelId))
                view.RefreshVisibleElements();
        }

        private void OnChannelUsersUpdated(ChatEvents.ChannelUsersStatusUpdated upd)
        {
            if (upd.Qualifies(currentChannelService.CurrentChannel))
                view.RefreshVisibleElements();
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

            onChannelSelectedSubscription = eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected);

            UpdateChannelMessages();
        }

        protected override void Deactivate()
        {
            view.Hide();

            onChannelSelectedSubscription?.Dispose();
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
    }
}
