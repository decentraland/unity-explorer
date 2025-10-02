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
using DCL.Translation;
using DCL.Translation.Service;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Chat
{
    public class ChatPanelPresenter : IDisposable
    {
        public event Action? PointerEntered;
        public event Action? PointerExited;

        private readonly ChatPanelView view;
        private readonly ChatSharedAreaEventBus chatSharedAreaEventBus;
        private readonly EventSubscriptionScope chatAreaEventBusScope = new ();

        private readonly CommandRegistry commandRegistry;
        private readonly ChatMemberListService chatMemberListService;
        private readonly ChatStateMachine chatStateMachine;
        private readonly EventSubscriptionScope uiScope;
        private readonly CommunityVoiceChatSubTitleButtonPresenter communityVoiceChatSubTitleButtonPresenter;
        private readonly ChatClickDetectionService chatClickDetectionService;

        private CancellationTokenSource initCts = new ();
        private bool isVisibleInSharedSpace => chatStateMachine is { IsMinimized: false, IsHidden: false };

        public ChatPanelPresenter(ChatPanelView view,
            ITextFormatter textFormatter,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            CurrentChannelService currentChannelService,
            CommunitiesDataProvider communityDataProvider,
            ChatConfig.ChatConfig chatConfig,
            IChatEventBus chatEventBus,
            IChatHistory chatHistory,
            CommunityDataService communityDataService,
            ChatMemberListService chatMemberListService,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            CommandRegistry commandRegistry,
            ChatInputBlockingService chatInputBlockingService,
            IEventBus eventBus,
            ChatContextMenuService chatContextMenuService,
            ChatClickDetectionService chatClickDetectionService,
            ChatSharedAreaEventBus chatSharedAreaEventBus,
            ITranslationSettings translationSettings,
            ITranslationMemory translationMemory,
            ITranslationCache translationCache)
        {
            this.view = view;
            this.chatSharedAreaEventBus = chatSharedAreaEventBus;
            this.chatMemberListService = chatMemberListService;
            this.commandRegistry = commandRegistry;
            this.chatClickDetectionService = chatClickDetectionService;

            uiScope = new EventSubscriptionScope();
            DCLInput.Instance.Shortcuts.OpenChatCommandLine.performed += OnOpenChatCommandLineShortcutPerformed;
            uiScope.Add(eventBus.Subscribe<ChatEvents.ChatStateChangedEvent>(OnChatStateChanged));

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
                translationSettings,
                commandRegistry.GetTitlebarViewModel,
                commandRegistry.GetCommunityThumbnail,
                commandRegistry.DeleteChatHistory,
                voiceChatOrchestrator,
                chatEventBus,
                commandRegistry.GetUserCallStatusCommand,
                commandRegistry.ToggleAutoTranslateCommand);


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
                chatConfig,
                currentChannelService,
                chatContextMenuService,
                translationMemory,
                translationCache,
                translationSettings,
                commandRegistry.GetMessageHistory,
                commandRegistry.CreateMessageViewModel,
                commandRegistry.MarkMessagesAsRead,
                commandRegistry.TranslateMessageCommand,
                commandRegistry.RevertToOriginalCommand);

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

            var memberListPresenter = new ChatMemberFeedPresenter(
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

            SubscribeToCoordinationEvents();
        }

        private void HandlePointerEnter(ChatSharedAreaEvents.ChatPanelPointerEnterEvent chatPanelPointerEnterEvent) => PointerEntered?.Invoke();

        private void HandlePointerExit(ChatSharedAreaEvents.ChatPanelPointerExitEvent chatPanelPointerExitEvent) => PointerExited?.Invoke();

        private void OnOpenChatCommandLineShortcutPerformed(InputAction.CallbackContext obj)
        {
            if (!chatStateMachine.IsFocused && (isVisibleInSharedSpace || chatStateMachine.IsMinimized))
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

        private void OnViewShow(ChatSharedAreaEvents.ChatPanelViewShowEvent evt)
        {
            initCts = new CancellationTokenSource();
            commandRegistry.InitializeChat.ExecuteAsync(initCts.Token).Forget();
            chatStateMachine.OnViewShow();
        }

        private void SetVisibility(ChatSharedAreaEvents.ChatPanelVisibilityEvent evt)
        {
            chatStateMachine.SetVisibility(evt.IsVisible);
        }

        private void SetFocusState(ChatSharedAreaEvents.ChatPanelFocusEvent evt)
        {
            chatStateMachine.SetFocusState();
        }

        private void ToggleState(ChatSharedAreaEvents.ChatPanelToggleEvent evt)
        {
            chatStateMachine.SetToggleState();
        }

        private void OnShownInSharedSpaceAsync(ChatSharedAreaEvents.ChatPanelShownInSharedSpaceEvent evt)
        {
            chatStateMachine.SetInitialState(evt.Focus);
        }

        private void OnMvcViewShowed(ChatSharedAreaEvents.ChatPanelMvcViewShowedEvent evt)
        {
            chatStateMachine.Minimize();
        }

        private void OnMvcViewClosed(ChatSharedAreaEvents.ChatPanelMvcViewClosedEvent evt)
        {
            chatStateMachine.PopState();
        }

        private void OnHiddenInSharedSpace(ChatSharedAreaEvents.ChatPanelHiddenInSharedSpaceEvent evt)
        {
            chatStateMachine.Minimize();
        }

        private void HandleClickInside(ChatSharedAreaEvents.ChatPanelClickInsideEvent evt)
        {
            chatClickDetectionService.ProcessRaycastResults(evt.RaycastResults);
        }

        private void HandleClickOutside(ChatSharedAreaEvents.ChatPanelClickOutsideEvent evt)
        {
            chatClickDetectionService.ProcessRaycastResults(evt.RaycastResults);
        }

        private void SubscribeToCoordinationEvents()
        {
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelPointerEnterEvent>(HandlePointerEnter));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelPointerExitEvent>(HandlePointerExit));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelFocusEvent>(SetFocusState));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelVisibilityEvent>(SetVisibility));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelToggleEvent>(ToggleState));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelViewShowEvent>(OnViewShow));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelShownInSharedSpaceEvent>(OnShownInSharedSpaceAsync));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelHiddenInSharedSpaceEvent>(OnHiddenInSharedSpace));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelMvcViewShowedEvent>(OnMvcViewShowed));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelMvcViewClosedEvent>(OnMvcViewClosed));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelClickInsideEvent>(HandleClickInside));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelClickOutsideEvent>(HandleClickOutside));
        }

        private void OnChatStateChanged(ChatEvents.ChatStateChangedEvent evt)
        {
            chatSharedAreaEventBus.RaiseVisibilityStateChangedEvent(isVisibleInSharedSpace);
        }
    }
}
