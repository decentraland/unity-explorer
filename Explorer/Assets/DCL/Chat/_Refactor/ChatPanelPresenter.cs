using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatFriends;
using DCL.Chat.ChatInput;
using DCL.Chat.ChatMessages;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatServices.ChatContextService;
using DCL.Chat.ChatStates;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.ChatArea;
using DCL.Communities;
using DCL.Communities.CommunitiesDataProvider;
using DCL.UI.InputFieldFormatting;
using DCL.UI.Profiles.Helpers;
using DCL.VoiceChat;
using System;
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
        private readonly ChatAreaEventBus chatAreaEventBus;
        private readonly EventSubscriptionScope chatAreaEventBusScope = new ();

        private readonly CommandRegistry commandRegistry;
        private readonly ChatMemberListService chatMemberListService;
        private readonly ChatStateMachine chatStateMachine;
        private readonly EventSubscriptionScope uiScope;
        private readonly CommunityVoiceChatSubTitleButtonPresenter communityVoiceChatSubTitleButtonPresenter;
        private readonly ChatClickDetectionService chatClickDetectionService;

        private CancellationTokenSource initCts = new ();
        public bool IsVisibleInSharedSpace => chatStateMachine is { IsMinimized: false, IsHidden: false };

        public ChatPanelPresenter(ChatPanelView view, ITextFormatter textFormatter, IVoiceChatOrchestrator voiceChatOrchestrator, CurrentChannelService currentChannelService, CommunitiesDataProvider communityDataProvider,
            ChatConfig.ChatConfig chatConfig, IChatEventBus chatEventBus, IChatHistory chatHistory, CommunityDataService communityDataService, ChatMemberListService chatMemberListService,
            ProfileRepositoryWrapper profileRepositoryWrapper, CommandRegistry commandRegistry, ChatInputBlockingService chatInputBlockingService, IEventBus eventBus, ChatContextMenuService chatContextMenuService,
            ChatClickDetectionService chatClickDetectionService, ChatAreaEventBus chatAreaEventBus)
        {
            this.view = view;
            this.chatAreaEventBus = chatAreaEventBus;
            this.chatMemberListService = chatMemberListService;
            this.commandRegistry = commandRegistry;
            this.chatClickDetectionService = chatClickDetectionService;

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

        private void HandlePointerEnter(ChatAreaEvents.ChatPanelPointerEnterEvent chatPanelPointerEnterEvent) => PointerEntered?.Invoke();

        private void HandlePointerExit(ChatAreaEvents.ChatPanelPointerExitEvent chatPanelPointerExitEvent) => PointerExited?.Invoke();

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

            chatAreaEventBusScope.Dispose();
        }

        private void OnViewShow(ChatAreaEvents.ChatPanelViewShowEvent evt)
        {
            initCts = new CancellationTokenSource();
            commandRegistry.InitializeChat.ExecuteAsync(initCts.Token).Forget();
            chatStateMachine.OnViewShow();
        }

        private void SetVisibility(ChatAreaEvents.ChatPanelVisibilityEvent evt)
        {
            chatStateMachine.SetVisibility(evt.IsVisible);
        }

        private void SetFocusState(ChatAreaEvents.ChatPanelFocusEvent evt)
        {
            chatStateMachine.SetFocusState();
        }

        private void ToggleState(ChatAreaEvents.ChatPanelToggleEvent evt)
        {
            chatStateMachine.SetToggleState();
        }

        private void OnShownInSharedSpaceAsync(ChatAreaEvents.ChatPanelShownInSharedSpaceEvent evt)
        {
            chatStateMachine.SetInitialState(evt.Focus);
        }

        private void OnMvcViewShowed(ChatAreaEvents.ChatPanelMvcViewShowedEvent evt)
        {
            chatStateMachine.Minimize();
        }

        private void OnMvcViewClosed(ChatAreaEvents.ChatPanelMvcViewClosedEvent evt)
        {
            chatStateMachine.PopState();
        }

        private void OnHiddenInSharedSpace(ChatAreaEvents.ChatPanelHiddenInSharedSpaceEvent evt)
        {
            chatStateMachine.Minimize();
        }

        private void HandleClickInside(ChatAreaEvents.ChatPanelClickInsideEvent evt)
        {
            chatClickDetectionService.ProcessRaycastResults(evt.RaycastResults);
        }

        private void HandleClickOutside(ChatAreaEvents.ChatPanelClickOutsideEvent evt)
        {
            chatClickDetectionService.ProcessRaycastResults(evt.RaycastResults);
        }

        private void SubscribeToCoordinationEvents()
        {
            chatAreaEventBusScope.Add(chatAreaEventBus.Subscribe<ChatAreaEvents.ChatPanelPointerEnterEvent>(HandlePointerEnter));
            chatAreaEventBusScope.Add(chatAreaEventBus.Subscribe<ChatAreaEvents.ChatPanelPointerExitEvent>(HandlePointerExit));
            chatAreaEventBusScope.Add(chatAreaEventBus.Subscribe<ChatAreaEvents.ChatPanelFocusEvent>(SetFocusState));
            chatAreaEventBusScope.Add(chatAreaEventBus.Subscribe<ChatAreaEvents.ChatPanelVisibilityEvent>(SetVisibility));
            chatAreaEventBusScope.Add(chatAreaEventBus.Subscribe<ChatAreaEvents.ChatPanelToggleEvent>(ToggleState));
            chatAreaEventBusScope.Add(chatAreaEventBus.Subscribe<ChatAreaEvents.ChatPanelViewShowEvent>(OnViewShow));
            chatAreaEventBusScope.Add(chatAreaEventBus.Subscribe<ChatAreaEvents.ChatPanelShownInSharedSpaceEvent>(OnShownInSharedSpaceAsync));
            chatAreaEventBusScope.Add(chatAreaEventBus.Subscribe<ChatAreaEvents.ChatPanelHiddenInSharedSpaceEvent>(OnHiddenInSharedSpace));
            chatAreaEventBusScope.Add(chatAreaEventBus.Subscribe<ChatAreaEvents.ChatPanelMvcViewShowedEvent>(OnMvcViewShowed));
            chatAreaEventBusScope.Add(chatAreaEventBus.Subscribe<ChatAreaEvents.ChatPanelMvcViewClosedEvent>(OnMvcViewClosed));
            chatAreaEventBusScope.Add(chatAreaEventBus.Subscribe<ChatAreaEvents.ChatPanelClickInsideEvent>(HandleClickInside));
            chatAreaEventBusScope.Add(chatAreaEventBus.Subscribe<ChatAreaEvents.ChatPanelClickOutsideEvent>(HandleClickOutside));
        }
    }
}
