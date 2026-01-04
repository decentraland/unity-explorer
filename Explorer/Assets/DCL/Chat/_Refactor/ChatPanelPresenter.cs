using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatFriends;
using DCL.Chat.ChatInput;
using DCL.Chat.ChatMessages;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatServices.ChatContextService;
using DCL.Chat.ChatStates;
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

        private readonly ChatCommandRegistry chatCommandRegistry;
        private readonly ChatMemberListService chatMemberListService;
        private readonly ChatStateMachine chatStateMachine;
        private readonly EventSubscriptionScope uiScope;
        private readonly CommunityVoiceChatSubTitleButtonPresenter communityVoiceChatSubTitleButtonPresenter;

        private CancellationTokenSource initCts = new ();
        private bool isVisible => chatStateMachine is { IsMinimized: false, IsHidden: false };

        public ChatPanelPresenter(ChatPanelView view,
            ITextFormatter textFormatter,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            CurrentChannelService currentChannelService,
            CommunitiesDataProvider communityDataProvider,
            ChatConfig.ChatConfig chatConfig,
            ChatEventBus chatEventBus,
            IChatHistory chatHistory,
            CommunityDataService communityDataService,
            ChatMemberListService chatMemberListService,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ChatCommandRegistry chatCommandRegistry,
            ChatInputBlockingService chatInputBlockingService,
            ChatContextMenuService chatContextMenuService,
            ChatClickDetectionHandler chatClickDetectionHandler,
            ChatSharedAreaEventBus chatSharedAreaEventBus,
            ITranslationSettings translationSettings,
            ITranslationMemory translationMemory,
            ITranslationCache translationCache)
        {
            this.view = view;
            this.chatSharedAreaEventBus = chatSharedAreaEventBus;
            this.chatMemberListService = chatMemberListService;
            this.chatCommandRegistry = chatCommandRegistry;

            uiScope = new EventSubscriptionScope();
            DCLInput.Instance.Shortcuts.OpenChatCommandLine.performed += OnOpenChatCommandLineShortcutPerformed;
            DCLInput.Instance.UI.Close.performed += OnUIClose;
            uiScope.Add(chatEventBus.Subscribe<ChatEvents.ChatStateChangedEvent>(OnChatStateChanged));

            communityVoiceChatSubTitleButtonPresenter = new CommunityVoiceChatSubTitleButtonPresenter(
                view.JoinCommunityLiveStreamSubTitleButton,
                voiceChatOrchestrator,
                currentChannelService.CurrentChannelProperty,
                communityDataProvider);


            var titleBarPresenter = new ChatTitlebarPresenter(
                view.TitlebarView,
                chatConfig,
                chatEventBus,
                communityDataService,
                currentChannelService,
                chatMemberListService,
                chatContextMenuService,
                translationSettings,
                chatCommandRegistry.GetTitlebarViewModel,
                chatCommandRegistry.GetCommunityThumbnail,
                chatCommandRegistry.DeleteChatHistory,
                voiceChatOrchestrator,
                chatEventBus,
                chatCommandRegistry.GetUserCallStatusCommand,
                chatCommandRegistry.ToggleAutoTranslateCommand);


            var channelListPresenter = new ChatChannelsPresenter(view.ConversationToolbarView2,
                chatEventBus,
                chatEventBus,
                chatHistory,
                currentChannelService,
                communityDataService,
                chatCommandRegistry.SelectChannel,
                chatCommandRegistry.CloseChannel,
                chatCommandRegistry.OpenConversation,
                chatCommandRegistry.CreateChannelViewModel);

            var messageFeedPresenter = new ChatMessageFeedPresenter(view.MessageFeedView,
                chatEventBus,
                chatHistory,
                chatConfig,
                currentChannelService,
                chatContextMenuService,
                translationMemory,
                translationCache,
                translationSettings,
                chatCommandRegistry.GetMessageHistory,
                chatCommandRegistry.CreateMessageViewModel,
                chatCommandRegistry.MarkMessagesAsRead,
                chatCommandRegistry.TranslateMessageCommand,
                chatCommandRegistry.RevertToOriginalCommand);

            var inputPresenter = new ChatInputPresenter(
                view.InputView,
                chatConfig,
                chatEventBus,
                currentChannelService,
                chatCommandRegistry.ResolveInputStateCommand,
                chatCommandRegistry.GetParticipantProfilesCommand,
                profileRepositoryWrapper,
                chatCommandRegistry.SendMessage,
                textFormatter);

            var memberListPresenter = new ChatMemberFeedPresenter(
                view.MemberListView,
                chatEventBus,
                chatEventBus,
                chatMemberListService,
                chatContextMenuService,
                chatCommandRegistry.GetChannelMembersCommand);

            uiScope.Add(titleBarPresenter);
            uiScope.Add(channelListPresenter);
            uiScope.Add(messageFeedPresenter);
            uiScope.Add(inputPresenter);
            uiScope.Add(memberListPresenter);
            uiScope.Add(chatClickDetectionHandler);

            var mediator = new ChatUIMediator(
                view,
                chatConfig,
                titleBarPresenter,
                channelListPresenter,
                messageFeedPresenter,
                inputPresenter,
                memberListPresenter,
                communityVoiceChatSubTitleButtonPresenter);

            chatStateMachine = new ChatStateMachine(chatEventBus,
                mediator,
                chatInputBlockingService,
                chatClickDetectionHandler,
                this);

            uiScope.Add(chatStateMachine);

            SubscribeToCoordinationEvents();
        }

        private void HandlePointerEnter(ChatSharedAreaEvents.PointerEnterChatPanelEvent pointerEnterChatPanelEvent) => PointerEntered?.Invoke();

        private void HandlePointerExit(ChatSharedAreaEvents.PointerExitChatPanelEvent pointerExitChatPanelEvent) => PointerExited?.Invoke();

        private void OnOpenChatCommandLineShortcutPerformed(InputAction.CallbackContext obj)
        {
            if (!chatStateMachine.IsFocused && (isVisible || chatStateMachine.IsMinimized))
            {
                chatStateMachine.SetFocusState();
                chatCommandRegistry.SelectChannel.SelectNearbyChannelAndInsertAsync("/", CancellationToken.None);
            }
        }

        private void OnUIClose(InputAction.CallbackContext obj)
        {
            if (chatStateMachine.IsMinimized) return;

            chatStateMachine.SetVisibility(true);
        }

        public void Dispose()
        {
            DCLInput.Instance.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;
            DCLInput.Instance.UI.Close.performed -= OnUIClose;

            initCts.SafeCancelAndDispose();

            uiScope.Dispose();

            chatMemberListService.Dispose();
            communityVoiceChatSubTitleButtonPresenter.Dispose();

            chatAreaEventBusScope.Dispose();
        }

        private void OnViewShow(ChatSharedAreaEvents.ChatPanelViewShowEvent evt)
        {
            initCts = new CancellationTokenSource();
            chatCommandRegistry.InitializeChat.ExecuteAsync(initCts.Token).Forget();
            chatStateMachine.OnViewShow();
        }

        private void SetFocusState(ChatSharedAreaEvents.FocusChatPanelEvent evt)
        {
            chatStateMachine.SetFocusState();
        }

        private void ToggleState(ChatSharedAreaEvents.ToggleChatPanelEvent evt)
        {
            chatStateMachine.SetToggleState();
        }

        private void OnFullscreenOpened(ChatSharedAreaEvents.FullscreenViewOpenEvent evt)
        {
            chatStateMachine.SetVisibility(false);
        }

        private void OnFullscreenClosed(ChatSharedAreaEvents.FullscreenClosedEvent evt)
        {
            if (!chatStateMachine.IsFocused)
                chatStateMachine.PopState();
        }

        private void SubscribeToCoordinationEvents()
        {
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.PointerEnterChatPanelEvent>(HandlePointerEnter));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.PointerExitChatPanelEvent>(HandlePointerExit));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.FocusChatPanelEvent>(SetFocusState));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ToggleChatPanelEvent>(ToggleState));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelViewShowEvent>(OnViewShow));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.FullscreenViewOpenEvent>(OnFullscreenOpened));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.FullscreenClosedEvent>(OnFullscreenClosed));
            chatAreaEventBusScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.UISubmitPerformedEvent>(OnUISubmitPerformed));
        }

        private void OnUISubmitPerformed(ChatSharedAreaEvents.UISubmitPerformedEvent obj)
        {
            if (!chatStateMachine.IsFocused)
                chatSharedAreaEventBus.RaiseFocusEvent();
        }

        private void OnChatStateChanged(ChatEvents.ChatStateChangedEvent evt)
        {
            chatSharedAreaEventBus.RaiseVisibilityStateChangedEvent(isVisible);
        }
    }
}
