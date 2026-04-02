using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatServices.ChatContextService;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Translation;
using DCL.Translation.Service;
using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.ChatReactions.Presenters;
using DCL.Web3;
using DG.Tweening;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using DCL.UI;
using DCL.UI.Controls.Configs;

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
        private readonly ITranslationCache translationCache;
        private readonly ITranslationSettings translationSettings;
        private readonly TranslateMessageCommand translateMessageCommand;
        private readonly RevertToOriginalCommand revertToOriginalCommand;
        private readonly ChatScrollToBottomPresenter scrollToBottomPresenter;
        private readonly ChatMessageReactionService messageReactionService;
        private readonly MessageReactionInteractionPresenter reactionInteraction;
        private readonly EventSubscriptionScope scope = new ();
        private readonly ChatConfig.ChatConfig chatConfig;

        private readonly List<ChatMessageViewModel> viewModels = new (500);
        private Dictionary<string, ChatMessageViewModel>? viewModelsMap;

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
            ChatConfig.ChatConfig chatConfig,
            CurrentChannelService currentChannelService,
            ChatContextMenuService contextMenuService,
            ITranslationMemory translationMemory,
            ITranslationCache translationCache,
            ITranslationSettings translationSettings,
            GetMessageHistoryCommand getMessageHistoryCommand,
            CreateMessageViewModelCommand createMessageViewModelCommand,
            MarkMessagesAsReadCommand markMessagesAsReadCommand,
            TranslateMessageCommand translateMessageCommand,
            RevertToOriginalCommand revertToOriginalCommand,
            ChatReactionsPresenter reactionsPresenter,
            ChatMessageReactionService messageReactionService,
            ReactionTooltipPresenter? tooltipPresenter = null)
        {
            this.view = view;
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.chatConfig = chatConfig;
            this.currentChannelService = currentChannelService;
            this.contextMenuService = contextMenuService;
            this.translationMemory = translationMemory;
            this.translationCache = translationCache;
            this.translationSettings = translationSettings;
            this.getMessageHistoryCommand = getMessageHistoryCommand;
            this.createMessageViewModelCommand = createMessageViewModelCommand;
            this.markMessagesAsReadCommand = markMessagesAsReadCommand;
            this.translateMessageCommand = translateMessageCommand;
            this.revertToOriginalCommand = revertToOriginalCommand;
            this.messageReactionService = messageReactionService;

            reactionInteraction = new MessageReactionInteractionPresenter(
                reactionsPresenter,
                messageReactionService,
                tooltipPresenter,
                msgId => currentChannelService.CurrentChannel?.GetReactions(msgId));

            scrollToBottomPresenter = new ChatScrollToBottomPresenter(view.ChatScrollToBottomView,
                currentChannelService);

            separatorViewModel = createMessageViewModelCommand.ExecuteForSeparator();

            view.Initialize(viewModels,
                translationSettings.IsTranslationFeatureActive,
                IsAutoTranslationEnabled,
                IsTranslationMemoryForMessageAvailable);

            view.OnTranslateMessageRequested += OnTranslateMessage;
            view.OnRevertMessageRequested += OnRevertMessage;

            translationSettings.OnAutoTranslationSettingsChanged += OnAutoTranslationSettingsChanged;
        }

        private bool IsAutoTranslationEnabled()
        {
            return translationSettings
                .GetAutoTranslateForConversation(currentChannelService.CurrentChannelId.Id);
        }

        private bool IsTranslationMemoryForMessageAvailable(string messageId)
        {
            return translationMemory.TryGet(messageId, out _);
        }

        private void ReleaseAndClearAllModels()
        {
            foreach (var vm in viewModels)
                // The separator is not pooled like the other messages, so it should not be released
                if (vm != separatorViewModel)
                    ChatMessageViewModel.RELEASE(vm);

            viewModels.Clear();
        }

        private void OnChatReset(ChatEvents.ChatResetEvent obj)
        {
            loadChannelCts.SafeCancelAndDispose();

            ReleaseAndClearAllModels();
            view.Clear();
            viewModelsMap = null;

            scrollToBottomPresenter.OnChannelChanged();
            separatorFixedIndexFromBottom = -1;
            messageCountWhenSeparatorViewed = null;
            isFocused = false;
        }

        public void Dispose()
        {
            scrollToBottomPresenter.Dispose();
            loadChannelCts.SafeCancelAndDispose();
            reactionInteraction.Dispose();

            view.OnTranslateMessageRequested -= OnTranslateMessage;
            view.OnRevertMessageRequested -= OnRevertMessage;
            translationSettings.OnAutoTranslationSettingsChanged -= OnAutoTranslationSettingsChanged;
        }

        private void OnTranslateMessage(string messageId)
        {
            var viewModel = FindViewModelById(messageId);
            if (viewModel == null || viewModel.TranslationState == TranslationState.Pending) return;

            // No need to check state here, the view already determined this is the correct action
            translateMessageCommand.Execute(viewModel.Message.MessageId,
                viewModel.Message.SenderWalletId,
                viewModel.Message.Message,
                CancellationToken.None);
        }

        private void OnRevertMessage(string messageId)
        {
            var viewModel = FindViewModelById(messageId);
            if (viewModel == null) return;

            // No need to check state here
            revertToOriginalCommand.Execute(viewModel.Message.MessageId);
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

            bool wasAtBottom = view.IsAtBottom();
            bool isSentByOwnUser = addedMessage is { IsSystemMessage: false, IsSentByOwnUser: true };

            int separatorIndexBeforeInsert = separatorFixedIndexFromBottom;
            ChatMessageViewModel newViewModel = InsertNewMessageViewModel(destinationChannel, addedMessage, index);
            bool needsSeparator = UpdateSeparatorAfterNewMessage(wasAtBottom, isSentByOwnUser, separatorIndexBeforeInsert);

            if (wasAtBottom)
                reactionInteraction.Deactivate();


            view.ReconstructScrollView(false);
            ScrollToBottomIfNeeded(needsSeparator);

            scrollToBottomPresenter.OnMessageReceived(isSentByOwnUser, wasAtBottom);

            if (!isFocused)
                view.RestartChatEntriesFadeout();
        }

        private ChatMessageViewModel InsertNewMessageViewModel(ChatChannel channel, ChatMessage message, int index)
        {
            (bool isTopMostMessage, ChatMessage? previousMessage) =
                GetMessageHistoryCommand.GetTopMostAndPreviousMessage(channel.Messages, index);

            ChatMessageViewModel vm = createMessageViewModelCommand.Execute(message, previousMessage, isTopMostMessage);
            vm.PendingToAnimate = true;

            if (translationMemory.TryGet(message.MessageId, out var translation))
            {
                vm.TranslationState = translation.State;
                vm.TranslatedText = translation.TranslatedBody;
            }

            RemoveNewMessagesSeparator();

            viewModels.Insert(index, vm);
            vm.Reactions = currentChannelService.CurrentChannel?.GetReactions(vm.Message.MessageId);

            if (viewModelsMap != null)
                viewModelsMap[vm.Message.MessageId] = vm;

            return vm;
        }

        /// <summary>
        /// Repositions the unread separator after a new message is inserted.
        /// Returns true if the separator was placed (meaning the user hasn't scrolled to bottom).
        /// </summary>
        private bool UpdateSeparatorAfterNewMessage(bool wasAtBottom, bool isSentByOwnUser, int previousSeparatorIndex)
        {
            bool separatorWasVisible = previousSeparatorIndex > -1;
            bool needsSeparator = !wasAtBottom && !isSentByOwnUser;

            if (needsSeparator)
            {
                if (messageCountWhenSeparatorViewed.HasValue)
                    markMessagesAsReadCommand.Execute(currentChannelService.CurrentChannel!, messageCountWhenSeparatorViewed.Value);

                TryAddNewMessagesSeparatorAfterPendingMessages();
            }
            else if (separatorWasVisible)
            {
                separatorFixedIndexFromBottom = previousSeparatorIndex + 1;
                viewModels.Insert(separatorFixedIndexFromBottom, separatorViewModel);
            }

            messageCountWhenSeparatorViewed = null;
            return needsSeparator;
        }

        private void ScrollToBottomIfNeeded(bool hasSeparator)
        {
            if (hasSeparator) return;

            markMessagesAsReadCommand.Execute(currentChannelService.CurrentChannel!);
            view.ShowLastMessage();
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

        private void OnChatContextMenuRequested(string messageId, ChatEntryView? chatEntry)
        {
            var viewModel = FindViewModelById(messageId);
            if (viewModel == null) return;

            var contextMenu = new GenericContextMenu(
                chatConfig.ContextMenuWidth,
                chatConfig.ContextMenuOffset,
                chatConfig.VerticalPadding,
                chatConfig.ElementsSpacing,
                anchorPoint: ContextMenuOpenDirection.TOP_LEFT);

            string textToCopy = viewModel.IsTranslated ? viewModel.TranslatedText : viewModel.Message.Message;
            contextMenu.AddControl(new ButtonContextMenuControlSettings(
                chatConfig.ChatContextMenuCopyText,
                chatConfig.CopyChatMessageContextMenuIcon,
                () =>
                {
                    ViewDependencies.ClipboardManager.CopyAndSanitize(this, textToCopy);
                }));

            if (translationSettings.IsTranslationFeatureActive())
            {
                if (viewModel.IsTranslated)
                {
                    contextMenu.AddControl(new ButtonContextMenuControlSettings(
                        chatConfig.ChatContextMenuSeeOriginalText,
                        chatConfig.SeeOriginalChatMessageContextMenuIcon,
                        () => revertToOriginalCommand.Execute(viewModel.Message.MessageId)
                    ));
                }
                else
                {
                    contextMenu.AddControl(new ButtonContextMenuControlSettings(
                        chatConfig.ChatContextMenuTranslateText,
                        chatConfig.TranslateChatMessageContextMenuIcon,
                        () => translateMessageCommand.Execute(viewModel.Message.MessageId,
                            viewModel.Message.SenderWalletId,
                            viewModel.Message.Message, CancellationToken.None)
                    ));
                }
            }

            var request = new ShowContextMenuRequest
            {
                MenuConfiguration = contextMenu, Position = chatEntry.messageBubbleElement.PopupPosition
            };

            contextMenuService
                .ShowCommunityContextMenuAsync(request)
                .Forget();
        }

        private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
        {
            scrollToBottomPresenter.OnChannelChanged();
            UpdateChannelMessages();
        }

        private void UpdateViewModelAndRefreshView(MessageTranslation? translation, string messageId)
        {
            var viewModel = FindViewModelById(messageId);
            if (viewModel == null) return;

            if (translation != null)
            {
                viewModel.TranslationState = translation.State;
                viewModel.TranslatedText = translation.TranslatedBody;
            }

            // Find the index of the ViewModel in the list
            int itemIndex = viewModels.IndexOf(viewModel);
            if (itemIndex == -1) return;

            // Tell the view to refresh this specific item
            view.RefreshItem(itemIndex);
        }

        private void OnMessageTranslationRequested(TranslationEvents.MessageTranslationRequested evt)
        {
            UpdateViewModelAndRefreshView(evt.Translation, evt.MessageId);
        }

        private void OnMessageTranslated(TranslationEvents.MessageTranslated evt)
        {
            UpdateViewModelAndRefreshView(evt.Translation, evt.MessageId);
        }

        private void OnMessageTranslationFailed(TranslationEvents.MessageTranslationFailed evt)
        {
            UpdateViewModelAndRefreshView(evt.Translation, evt.MessageId);
        }

        private void OnMessageTranslationReverted(TranslationEvents.MessageTranslationReverted evt)
        {
            UpdateViewModelAndRefreshView(evt.Translation, evt.MessageId);
        }

        private void UpdateChannelMessages()
        {
            if (currentChannelService.UserStateService == null)
            {
                ReportHub.LogWarning(ReportCategory.CHAT_HISTORY, $"{nameof(UpdateChannelMessages)} called but User State Service is NOT set. Aborting.");
                return;
            }

            loadChannelCts = loadChannelCts.SafeRestart();

            RemoveNewMessagesSeparator();

            // When the history the state is not final so the events should be ignored
            Unsubscribe();

            LoadChannelHistoryAsync(loadChannelCts.Token).Forget();

            async UniTaskVoid LoadChannelHistoryAsync(CancellationToken ct)
            {
                try
                {
                    ReleaseAndClearAllModels();
                    await getMessageHistoryCommand.ExecuteAsync(viewModels, currentChannelService.CurrentChannelId, ct);

                    BindReactionsToViewModels();

                    TryAddNewMessagesSeparatorAfterPendingMessages();

                    RebuildFastIndexIfNeeded();

                    Subscribe();

                    view.SetUserConnectivityProvider(currentChannelService.UserStateService.OnlineParticipants);

                    view.SetDmPartnerWallet(
                        currentChannelService.CurrentChannel?.ChannelType == ChatChannel.ChatChannelType.USER
                            ? currentChannelService.CurrentChannelId.Id
                            : null);

                    view.ReconstructScrollView(true);
                    ScrollToNewMessagesSeparator();

                    ChatChannel currentChannel = currentChannelService.CurrentChannel!;
                    int unreadCount = currentChannel.Messages.Count - currentChannel.ReadMessages;

                    if (unreadCount > 0)
                        if (view.IsAtBottom())
                            MarkCurrentChannelAsRead();
                        else
                            view.SetScrollToBottomButtonVisibility(true, unreadCount, false);
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
                ClearTranslationsForCurrentChannel();

                view.SetScrollToBottomButtonVisibility(false, 0, false);
                RemoveNewMessagesSeparator();
                ReleaseAndClearAllModels();
                view.Clear();
                viewModelsMap?.Clear();
            }
        }

        private void Subscribe()
        {
            view.OnChatContextMenuRequested += OnChatContextMenuRequested;
            view.OnProfileContextMenuRequested += OnProfileContextMenuRequested;
            view.OnScrolledToBottom += MarkCurrentChannelAsRead;
            view.OnScrollPositionChanged += OnScrollPositionChanged;
            view.OnScrollToBottomButtonClicked += OnScrollToBottomButtonClicked;
            view.OnReactionButtonClicked += reactionInteraction.OnReactionButtonClicked;
            view.OnReactionPillClicked += reactionInteraction.OnReactionPillClicked;
            view.OnReactionHoverEnter += reactionInteraction.OnReactionHoverEnter;
            view.OnReactionHoverExit += reactionInteraction.OnReactionHoverExit;

            scope.Add(eventBus.Subscribe<ChatEvents.ChatHistoryClearedEvent>(OnChatHistoryCleared));
            scope.Add(eventBus.Subscribe<ChatEvents.ChannelUsersStatusUpdated>(OnChannelUsersUpdated));
            scope.Add(eventBus.Subscribe<ChatEvents.UserStatusUpdatedEvent>(OnUserStatusUpdated));
            scope.Add(eventBus.Subscribe<TranslationEvents.MessageTranslationRequested>(OnMessageTranslationRequested));
            scope.Add(eventBus.Subscribe<TranslationEvents.MessageTranslated>(OnMessageTranslated));
            scope.Add(eventBus.Subscribe<TranslationEvents.MessageTranslationFailed>(OnMessageTranslationFailed));
            scope.Add(eventBus.Subscribe<TranslationEvents.MessageTranslationReverted>(OnMessageTranslationReverted));
            scope.Add(eventBus.Subscribe<ChatEvents.ChatResetEvent>(OnChatReset));

            scrollToBottomPresenter.RequestScrollAction += OnRequestScrollAction;
            chatHistory.MessageAdded += OnMessageAddedToChatHistory;

            if (currentChannelService.CurrentChannel != null)
                currentChannelService.CurrentChannel.ReactionChanged += OnReactionChanged;
        }

        private void Unsubscribe()
        {
            view.OnChatContextMenuRequested -= OnChatContextMenuRequested;
            view.OnProfileContextMenuRequested -= OnProfileContextMenuRequested;
            view.OnScrolledToBottom -= MarkCurrentChannelAsRead;
            view.OnScrollPositionChanged -= OnScrollPositionChanged;
            view.OnScrollToBottomButtonClicked -= OnScrollToBottomButtonClicked;
            view.OnReactionButtonClicked -= reactionInteraction.OnReactionButtonClicked;
            view.OnReactionPillClicked -= reactionInteraction.OnReactionPillClicked;
            view.OnReactionHoverEnter -= reactionInteraction.OnReactionHoverEnter;
            view.OnReactionHoverExit -= reactionInteraction.OnReactionHoverExit;
            reactionInteraction.Deactivate();

            scope.Dispose();
            scrollToBottomPresenter.RequestScrollAction -= OnRequestScrollAction;
            chatHistory.MessageAdded -= OnMessageAddedToChatHistory;

            if (currentChannelService.CurrentChannel != null)
                currentChannelService.CurrentChannel.ReactionChanged -= OnReactionChanged;
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

        private void OnReactionChanged(string messageId)
        {
            var viewModel = FindViewModelById(messageId);
            if (viewModel == null) return;

            viewModel.Reactions = currentChannelService.CurrentChannel?.GetReactions(messageId);
            reactionInteraction.HideTooltip();
            view.ReconstructScrollView(false);
        }

        private void OnRequestScrollAction()
        {
            view.ShowLastMessage(useSmoothScroll: true);
        }

        private void OnScrollToBottomButtonClicked()
        {
            view.ShowLastMessage(useSmoothScroll: true);
        }

        protected override void Activate()
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

        private void OnAutoTranslationSettingsChanged(string conversationId)
        {
            if (currentChannelService.CurrentChannelId.Id == conversationId)
                RebuildFastIndexIfNeeded();

            view.RefreshVisibleElements();
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

        private bool UseFastIndexForCurrentConversation()
        {
            // Gate on per-conversation Auto-Translate setting (your requirement).
            // Keep this exactly in one place so policy changes are trivial.
            return IsAutoTranslationEnabled();
        }

        private void RebuildFastIndexIfNeeded()
        {
            if (!UseFastIndexForCurrentConversation())
            {
                viewModelsMap = null;
                return;
            }

            viewModelsMap ??= new Dictionary<string, ChatMessageViewModel>(Math.Max(16, viewModels.Count));

            viewModelsMap.Clear();
            for (int i = 0; i < viewModels.Count; i++)
            {
                var vm = viewModels[i];
                if (ReferenceEquals(vm, separatorViewModel))
                    continue;

                string id = vm.Message.MessageId;
                if (!string.IsNullOrEmpty(id))
                    viewModelsMap[id] = vm; // O(1) lookup
            }
        }

        private ChatMessageViewModel? LinearFindViewModelById(string messageId)
        {
            // Cheaper than LINQ; avoids delegate/iterator allocations.
            for (int i = 0; i < viewModels.Count; i++)
            {
                var vm = viewModels[i];
                if (vm.Message.MessageId == messageId)
                    return vm;
            }

            return null;
        }

        private ChatMessageViewModel? FindViewModelById(string messageId)
        {
            if (viewModelsMap != null && viewModelsMap.TryGetValue(messageId, out var vm))
                return vm;

            return LinearFindViewModelById(messageId); // safe fallback when index is disabled
        }

        private void BindReactionsToViewModels()
        {
            ChatChannel? channel = currentChannelService.CurrentChannel;
            if (channel == null) return;

            messageReactionService.RegisterChannelMessages(channel);

            for (int i = 0; i < viewModels.Count; i++)
            {
                var vm = viewModels[i];
                if (ReferenceEquals(vm, separatorViewModel)) continue;

                vm.Reactions = channel.GetReactions(vm.Message.MessageId);
            }
        }

        private void ClearTranslationsForCurrentChannel()
        {
            // Bail if translation feature entirely disabled (optional; safe to clear anyway)
            // if (!translationSettings.IsTranslationFeatureActive()) return;

            // Gather all message IDs from the feed (skip the separator)
            var ids = new HashSet<string>(viewModels.Count);
            foreach (var vm in viewModels)
            {
                if (ReferenceEquals(vm, separatorViewModel)) continue;
                if (!string.IsNullOrEmpty(vm.Message.MessageId)) ids.Add(vm.Message.MessageId);
            }

            foreach (string? id in ids)
            {
                translationMemory.Remove(id);
                translationCache.RemoveAllForMessage(id);
            }
        }
    }
}
