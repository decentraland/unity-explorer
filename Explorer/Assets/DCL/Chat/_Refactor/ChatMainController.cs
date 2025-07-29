using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;
using DCL.Chat._Refactor.ChatStates;
using DCL.Chat.ChatMediator;
using DCL.Chat.ChatMessages;
using DCL.Chat.ChatUseCases;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.Services;
using DCL.Chat.Services.DCL.Chat;
using DCL.Settings.Settings;
using DCL.UI.Profiles.Helpers;

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
        private readonly ICurrentChannelService currentChannelService;
        private readonly ChatUserStateBridge chatUserStateBridge;
        private readonly IChatUserStateEventBus userStateEventBus;
        private readonly ChatConfig chatConfig;
        private readonly IChatHistory chatHistory;
        private readonly IChatEventBus chatEventBus;
        private readonly IChatMessagesBus chatMessagesBus;
        private ChatStateMachine? chatStateMachine;
        private EventSubscriptionScope uiScope;
        private readonly ChatContextMenuService chatContextMenuService;
        private readonly ChatClickDetectionService chatClickDetectionService;
        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public event Action? PointerEntered;
        public event Action? PointerExited;

        public bool IsVisibleInSharedSpace => chatStateMachine != null && chatStateMachine!.IsFocused;

        public ChatMainController(ViewFactoryMethod viewFactory,
            ChatConfig chatConfig,
            IEventBus eventBus,
            IChatMessagesBus chatMessagesBus,
            IChatEventBus chatEventBus,
            IChatUserStateEventBus userStateEventBus,
            ChatUserStateBridge chatUserStateBridge,
            ICurrentChannelService currentChannelService,
            ChatInputBlockingService chatInputBlockingService,
            ChatSettingsAsset chatSettingsAsset,
            CommandRegistry commandRegistry,
            IChatHistory chatHistory,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ChatMemberListService chatMemberListService,
            ChatContextMenuService chatContextMenuService,
            ChatClickDetectionService chatClickDetectionService) : base(viewFactory)
        {
            this.chatConfig = chatConfig;
            this.eventBus = eventBus;
            this.chatMessagesBus = chatMessagesBus;
            this.userStateEventBus = userStateEventBus;
            this.chatEventBus = chatEventBus;
            this.chatUserStateBridge = chatUserStateBridge;
            this.currentChannelService = currentChannelService;
            this.chatInputBlockingService = chatInputBlockingService;
            this.commandRegistry = commandRegistry;
            this.chatHistory = chatHistory;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.chatMemberListService = chatMemberListService;
            this.chatContextMenuService = chatContextMenuService;
            this.chatClickDetectionService = chatClickDetectionService;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        private CancellationTokenSource initCts;
        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            uiScope = new EventSubscriptionScope();

            viewInstance!.OnPointerEnterEvent += HandlePointerEnter;
            viewInstance.OnPointerExitEvent += HandlePointerExit;

            chatMemberListService.Start();

            var titleBarPresenter = new ChatTitlebarPresenter(viewInstance.TitlebarView,
                chatConfig,
                eventBus,
                currentChannelService,
                chatMemberListService,
                chatContextMenuService,
                chatClickDetectionService,
                commandRegistry.GetTitlebarViewModel,
                commandRegistry.DeleteChatHistory);

            var channelListPresenter = new ChatChannelsPresenter(viewInstance.ConversationToolbarView2,
                eventBus,
                chatMessagesBus,
                chatEventBus,
                userStateEventBus,
                chatHistory,
                currentChannelService,
                profileRepositoryWrapper,
                commandRegistry.SelectChannel,
                commandRegistry.LeaveChannel,
                commandRegistry.OpenPrivateConversation,
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
                commandRegistry.GetParticipantProfilesCommand,
                profileRepositoryWrapper,
                commandRegistry.SendMessage);

            var memberListPresenter = new ChatMemberListPresenter(
                viewInstance.MemberListView,
                eventBus,
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
                memberListPresenter);

            chatStateMachine = new ChatStateMachine(eventBus,
                mediator,
                chatInputBlockingService,
                chatClickDetectionService,
                this);

            uiScope.Add(chatStateMachine);
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

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ChatControllerShowParams showParams)
        {
            if (State != ControllerState.ViewHidden)
            {
                chatStateMachine?.SetInitialState(showParams.Focus);
                ViewShowingComplete?.Invoke(this);
            }
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
                viewInstance.OnPointerEnterEvent -= HandlePointerEnter;
                viewInstance.OnPointerExitEvent -= HandlePointerExit;
            }

            base.Dispose();
            initCts?.Cancel();
            initCts?.Dispose();
            uiScope?.Dispose();

            chatMemberListService.Dispose();
        }
    }
}
