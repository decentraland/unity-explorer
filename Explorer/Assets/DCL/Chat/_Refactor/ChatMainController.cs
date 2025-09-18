using System;
using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatFriends;
using DCL.Chat.ChatInput;
using DCL.Chat.ChatMessages;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatServices.ChatContextService;
using DCL.Chat.ChatStates;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Communities.CommunitiesDataProvider;
using DCL.UI.Profiles.Helpers;
using DCL.VoiceChat;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Chat
{
    public class ChatMainController : ControllerBase<ChatMainView, ChatControllerShowParams>,
                                  IControllerInSharedSpace<ChatMainView, ChatControllerShowParams>
    {
        private readonly IEventBus eventBus;
        private readonly ChatInputBlockingService chatInputBlockingService;
        private readonly CommandRegistry commandRegistry;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ChatMemberListService chatMemberListService;
        private readonly CommunityDataService communityDataService;
        private readonly CurrentChannelService currentChannelService;
        private readonly ChatConfig.ChatConfig chatConfig;
        private readonly IChatHistory chatHistory;
        private readonly IChatEventBus chatEventBus;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly IMVCManager mvcManager;
        private readonly ChatContextMenuService chatContextMenuService;
        private readonly ChatClickDetectionService chatClickDetectionService;
        private readonly CommunitiesDataProvider communityDataProvider;

        private ChatStateMachine? chatStateMachine;
        private EventSubscriptionScope uiScope;
        private CommunityVoiceChatSubTitleButtonPresenter communityVoiceChatSubTitleButtonPresenter;

        private readonly HashSet<IBlocksChat> chatBlockers = new ();

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public event Action? PointerEntered;
        public event Action? PointerExited;

        public bool IsVisibleInSharedSpace => chatStateMachine is { IsMinimized: false, IsHidden: false };

        public bool IsFocused => chatStateMachine is { IsFocused: true };

        public ChatMainController(ViewFactoryMethod viewFactory,
            ChatConfig.ChatConfig chatConfig,
            IEventBus eventBus,
            IMVCManager mvcManager,
            IChatEventBus chatEventBus,
            CurrentChannelService currentChannelService,
            ChatInputBlockingService chatInputBlockingService,
            CommandRegistry commandRegistry,
            IChatHistory chatHistory,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ChatMemberListService chatMemberListService,
            ChatContextMenuService chatContextMenuService,
            CommunityDataService communityDataService,
            ChatClickDetectionService chatClickDetectionService,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            CommunitiesDataProvider communityDataProvider) : base(viewFactory)
        {
            this.chatConfig = chatConfig;
            this.eventBus = eventBus;
            this.mvcManager = mvcManager;
            this.chatEventBus = chatEventBus;
            this.currentChannelService = currentChannelService;
            this.chatInputBlockingService = chatInputBlockingService;
            this.commandRegistry = commandRegistry;
            this.chatHistory = chatHistory;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.chatMemberListService = chatMemberListService;
            this.chatContextMenuService = chatContextMenuService;
            this.communityDataService = communityDataService;
            this.chatClickDetectionService = chatClickDetectionService;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.communityDataProvider = communityDataProvider;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        private CancellationTokenSource initCts;
        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            uiScope = new EventSubscriptionScope();

            mvcManager.OnViewShowed += OnMvcViewShowed;
            mvcManager.OnViewClosed += OnMvcViewClosed;
            viewInstance!.OnPointerEnterEvent += HandlePointerEnter;
            viewInstance.OnPointerExitEvent += HandlePointerExit;
            DCLInput.Instance.Shortcuts.OpenChatCommandLine.performed += OnOpenChatCommandLineShortcutPerformed;
            DCLInput.Instance.UI.Close.performed += OnUIClose;

            communityVoiceChatSubTitleButtonPresenter = new CommunityVoiceChatSubTitleButtonPresenter(
                viewInstance.JoinCommunityLiveStreamSubTitleButton,
                voiceChatOrchestrator,
                currentChannelService.CurrentChannelProperty,
                communityDataProvider);


            var titleBarPresenter = new ChatTitlebarPresenter(
                viewInstance.TitlebarView,
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


            var channelListPresenter = new ChatChannelsPresenter(viewInstance.ConversationToolbarView2,
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

            var messageFeedPresenter = new ChatMessageFeedPresenter(viewInstance.MessageFeedView,
                eventBus,
                chatHistory,
                currentChannelService,
                chatContextMenuService,
                commandRegistry.GetMessageHistory,
                commandRegistry.CreateMessageViewModel,
                commandRegistry.MarkMessagesAsRead);

            var inputPresenter = new ChatInputPresenter(
                viewInstance.InputView,
                chatConfig,
                eventBus,
                chatEventBus,
                currentChannelService,
                commandRegistry.ResolveInputStateCommand,
                commandRegistry.GetParticipantProfilesCommand,
                profileRepositoryWrapper,
                commandRegistry.SendMessage);

            var memberListPresenter = new ChatMemberListPresenter(
                viewInstance.MemberListView,
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
                viewInstance,
                chatConfig,
                titleBarPresenter,
                channelListPresenter,
                messageFeedPresenter,
                inputPresenter,
                memberListPresenter,
                communityVoiceChatSubTitleButtonPresenter);

            chatStateMachine = new ChatStateMachine(eventBus,
                mediator,
                chatInputBlockingService,
                chatClickDetectionService,
                this);

            uiScope.Add(chatStateMachine);
        }

        private void OnOpenChatCommandLineShortcutPerformed(InputAction.CallbackContext obj)
        {
            if (chatStateMachine == null) return;

            if (!chatStateMachine.IsFocused && (IsVisibleInSharedSpace || chatStateMachine.IsMinimized))
            {
                chatStateMachine.SetFocusState();
                commandRegistry.SelectChannel.SelectNearbyChannelAndInsertAsync("/", CancellationToken.None);
            }
        }

        private void OnUIClose(InputAction.CallbackContext obj)
        {
            if (chatStateMachine == null) return;
            if (chatStateMachine.IsMinimized) return;
            if (chatBlockers.Count > 0) return;

            chatStateMachine?.SetVisibility(true);
        }

        protected override void OnViewShow()
        {
            initCts = new CancellationTokenSource();
            commandRegistry.InitializeChat.ExecuteAsync(initCts.Token).Forget();
            chatStateMachine?.OnViewShow();
        }

        public void SetVisibility(bool isVisible)
        {
            chatStateMachine?.SetVisibility(isVisible);
        }

        public void SetFocusState()
        {
            chatStateMachine?.SetFocusState();
        }

        public void ToggleState()
        {
            chatStateMachine?.SetToggleState();
        }

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ChatControllerShowParams showParams)
        {
            // This method is called when we want to "show" the chat.
            // This can happen when:
            // 1. Toggling from a Minimized state.
            // 2. Another panel (like Friends) that was obscuring the chat is closed.
            if (State == ControllerState.ViewHidden)
            {
                // If the entire controller view is not even active, we can't proceed.
                await UniTask.CompletedTask;
                return;
            }

            // If the chat was fully hidden (e.g., by the Friends panel), transition to Default.
            // If it was minimized, transition to Default or Focused based on the input.
            // The `showParams.Focus` will be true when toggling with Enter/shortcut, and false when returning from another panel.
            chatStateMachine?.SetInitialState(showParams.Focus);

            ViewShowingComplete?.Invoke(this);
            await UniTask.CompletedTask;
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            chatStateMachine?.Minimize();
            await UniTask.CompletedTask;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }

        private void HandlePointerEnter() => PointerEntered?.Invoke();
        private void HandlePointerExit() => PointerExited?.Invoke();

        public override void Dispose()
        {
            if (viewInstance != null)
            {
                mvcManager.OnViewShowed -= OnMvcViewShowed;
                mvcManager.OnViewClosed -= OnMvcViewClosed;
                viewInstance.OnPointerEnterEvent -= HandlePointerEnter;
                viewInstance.OnPointerExitEvent -= HandlePointerExit;
                DCLInput.Instance.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;
                DCLInput.Instance.UI.Close.performed -= OnUIClose;
            }

            base.Dispose();

            if (initCts != null)
            {
                initCts.Cancel();
                initCts.Dispose();
                initCts = null;
            }

            uiScope?.Dispose();

            chatMemberListService.Dispose();
            communityVoiceChatSubTitleButtonPresenter?.Dispose();
            chatBlockers.Clear();
        }

        private void OnMvcViewShowed(IController controller)
        {
            if (controller is not IBlocksChat blocker) return;

            chatStateMachine?.Minimize();
            chatBlockers.Add(blocker);
        }

        private void OnMvcViewClosed(IController controller)
        {
            if (controller is not IBlocksChat blocker) return;

            chatBlockers.Remove(blocker);

            if (chatBlockers.Count == 0)
                chatStateMachine?.PopState();
        }
    }
}
