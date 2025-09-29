using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatFriends;
using DCL.Chat.ChatInput;
using DCL.Chat.ChatMessages;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatServices.ChatContextService;
using DCL.Chat.ChatStates;
using DCL.Chat;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.ChatArea;
using DCL.Communities;
using DCL.Communities.CommunitiesDataProvider;
using DCL.UI.InputFieldFormatting;
using DCL.UI.Profiles.Helpers;
using DCL.VoiceChat;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Chat
{
    public class ChatPanelPresenter : IDisposable
    {
        public event Action? PointerEntered;
        public event Action? PointerExited;

        private readonly ChatPanelView view;
        private readonly ChatCoordinationEventBus coordinationEventBus;

        private readonly CommandRegistry commandRegistry;
        private readonly ChatMemberListService chatMemberListService;
        private readonly ChatStateMachine chatStateMachine;
        private readonly EventSubscriptionScope uiScope;
        private readonly CommunityVoiceChatSubTitleButtonPresenter communityVoiceChatSubTitleButtonPresenter;

        private CancellationTokenSource initCts = new ();
        public bool IsVisibleInSharedSpace => chatStateMachine is { IsMinimized: false, IsHidden: false };
        private readonly List<IDisposable> eventSubscriptions = new();

        public ChatPanelPresenter(ChatPanelView view, ITextFormatter textFormatter, IVoiceChatOrchestrator voiceChatOrchestrator, CurrentChannelService currentChannelService, CommunitiesDataProvider communityDataProvider,
            ChatConfig.ChatConfig chatConfig, IChatEventBus chatEventBus, IChatHistory chatHistory, CommunityDataService communityDataService, ChatMemberListService chatMemberListService,
            ProfileRepositoryWrapper profileRepositoryWrapper, CommandRegistry commandRegistry, ChatInputBlockingService chatInputBlockingService, IEventBus eventBus, ChatContextMenuService chatContextMenuService,
            ChatClickDetectionService chatClickDetectionService, ChatCoordinationEventBus coordinationEventBus)
        {
            this.view = view;
            this.coordinationEventBus = coordinationEventBus;
            this.chatMemberListService = chatMemberListService;
            this.commandRegistry = commandRegistry;

            uiScope = new EventSubscriptionScope();
            DCLInput.Instance.Shortcuts.OpenChatCommandLine.performed += OnOpenChatCommandLineShortcutPerformed;

            communityVoiceChatSubTitleButtonPresenter = new CommunityVoiceChatSubTitleButtonPresenter(
                view.JoinCommunityLiveStreamSubTitleButton,
                voiceChatOrchestrator,
                currentChannelService.CurrentChannelProperty,
                communityDataProvider);


            var titleBarPresenter = new ChatTitlebarPresenter(
                view.TitlebarView,
                chatConfig,
                eventBus,
                communityDataService,
                currentChannelService,
                chatMemberListService,
                chatContextMenuService,
                commandRegistry.GetTitlebarViewModel,
                commandRegistry.GetCommunityThumbnail,
                commandRegistry.DeleteChatHistory,
                voiceChatOrchestrator,
                chatEventBus,
                commandRegistry.GetUserCallStatusCommand);


            var channelListPresenter = new ChatChannelsPresenter(view.ConversationToolbarView2,
                eventBus,
                chatEventBus,
                chatHistory,
                currentChannelService,
                communityDataService,
                profileRepositoryWrapper,
                commandRegistry.SelectChannel,
                commandRegistry.CloseChannel,
                commandRegistry.OpenConversation,
                commandRegistry.CreateChannelViewModel);

            var messageFeedPresenter = new ChatMessageFeedPresenter(view.MessageFeedView,
                eventBus,
                chatHistory,
                currentChannelService,
                chatContextMenuService,
                commandRegistry.GetMessageHistory,
                commandRegistry.CreateMessageViewModel,
                commandRegistry.MarkMessagesAsRead);

            var inputPresenter = new ChatInputPresenter(
                view.InputView,
                chatConfig,
                eventBus,
                chatEventBus,
                currentChannelService,
                commandRegistry.ResolveInputStateCommand,
                commandRegistry.GetParticipantProfilesCommand,
                profileRepositoryWrapper,
                commandRegistry.SendMessage,
                textFormatter);

            var memberListPresenter = new ChatMemberListPresenter(
                view.MemberListView,
                eventBus,
                chatEventBus,
                chatMemberListService,
                chatContextMenuService,
                commandRegistry.GetChannelMembersCommand);

            uiScope.Add(titleBarPresenter);
            uiScope.Add(channelListPresenter);
            uiScope.Add(messageFeedPresenter);
            uiScope.Add(inputPresenter);
            uiScope.Add(memberListPresenter);
            uiScope.Add(chatClickDetectionService);

            var mediator = new ChatUIMediator(
                view,
                chatConfig,
                titleBarPresenter,
                channelListPresenter,
                messageFeedPresenter,
                inputPresenter,
                memberListPresenter,
                communityVoiceChatSubTitleButtonPresenter,
                voiceChatOrchestrator);

            chatStateMachine = new ChatStateMachine(eventBus,
                mediator,
                chatInputBlockingService,
                chatClickDetectionService,
                this);

            uiScope.Add(chatStateMachine);

            // Subscribe to coordination events
            SubscribeToCoordinationEvents();
        }

        internal void HandlePointerEnter() => PointerEntered?.Invoke();

        internal void HandlePointerExit() => PointerExited?.Invoke();

        private void OnOpenChatCommandLineShortcutPerformed(InputAction.CallbackContext obj)
        {
            if (!chatStateMachine.IsFocused && (IsVisibleInSharedSpace || chatStateMachine.IsMinimized))
            {
                chatStateMachine.SetFocusState();
                commandRegistry.SelectChannel.SelectNearbyChannelAndInsertAsync("/", CancellationToken.None);
            }
        }

        public void OnUIClose()
        {
            if (chatStateMachine.IsMinimized) return;
            chatStateMachine.SetVisibility(true);
        }

        public void Dispose()
        {
            DCLInput.Instance.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;

            initCts.SafeCancelAndDispose();

            uiScope.Dispose();

            chatMemberListService.Dispose();
            communityVoiceChatSubTitleButtonPresenter.Dispose();

            // Dispose event subscriptions
            foreach (var subscription in eventSubscriptions)
                subscription.Dispose();
            eventSubscriptions.Clear();
        }

        public void OnViewShow()
        {
            initCts = new CancellationTokenSource();
            commandRegistry.InitializeChat.ExecuteAsync(initCts.Token).Forget();
            chatStateMachine.OnViewShow();
        }

        public void SetVisibility(bool isVisible)
        {
            chatStateMachine.SetVisibility(isVisible);
        }

        public void SetFocusState()
        {
            chatStateMachine.SetFocusState();
        }

        public void ToggleState()
        {
            chatStateMachine.SetToggleState();
        }

        public void OnShownInSharedSpaceAsync(bool focus)
        {
            chatStateMachine.SetInitialState(focus);
        }

        public void OnMvcViewShowed()
        {
            chatStateMachine.Minimize();
        }

        public void OnMvcViewClosed()
        {
            chatStateMachine.PopState();
        }

        public void OnHiddenInSharedSpace()
        {
            chatStateMachine.Minimize();
        }

        private void SubscribeToCoordinationEvents()
        {

            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelPointerEnterEvent>(_ => HandlePointerEnter()));
            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelPointerExitEvent>(_ => HandlePointerExit()));
            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelFocusEvent>(_ => SetFocusState()));
            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelVisibilityEvent>(evt => SetVisibility(evt.IsVisible)));
            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelToggleEvent>(_ => ToggleState()));
            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelViewShowEvent>(_ => OnViewShow()));
            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelShownInSharedSpaceEvent>(evt => OnShownInSharedSpaceAsync(evt.Focus)));
            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelHiddenInSharedSpaceEvent>(_ => OnHiddenInSharedSpace()));
            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelMvcViewShowedEvent>(_ => OnMvcViewShowed()));
            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelMvcViewClosedEvent>(_ => OnMvcViewClosed()));
        }
    }
}
