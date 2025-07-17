using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;
using DCL.Chat._Refactor.ChatStates;
using DCL.Chat.ChatMediator;
using DCL.Chat.ChatUseCases;
using DCL.Chat.History;
using DCL.Chat.Services;
using DCL.Chat.Services.DCL.Chat;
using DCL.Settings.Settings;
using DCL.UI.Profiles.Helpers;
using UnityEngine;
using Utilities;

namespace DCL.Chat
{
    public class ChatMainController : ControllerBase<ChatMainView, ChatControllerShowParams>,
                                  IControllerInSharedSpace<ChatMainView, ChatControllerShowParams>
    {
        private readonly IEventBus eventBus;
        private readonly ChatInputBlockingService chatInputBlockingService;
        private readonly ChatSettingsAsset chatSettingsAsset;
        private readonly CommandRegistry commandRegistry;
        private readonly IChatHistory chatHistory;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ChatMemberListService chatMemberListService;
        private readonly ICurrentChannelService currentChannelService;
        private readonly ChatConfig chatConfig;
        private ChatStateMachine? chatStateMachine;
        private EventSubscriptionScope uiScope;
        
        private ChatClickDetectionService chatClickDetectionService;
        private ChatUserStateBridge chatUserStateBridge;
        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public event Action? PointerEntered;
        public event Action? PointerExited;

        public bool IsVisibleInSharedSpace =>
            State != ControllerState.ViewHidden;
        

        public ChatMainController(ViewFactoryMethod viewFactory,
            ChatConfig chatConfig,
            IEventBus eventBus,
            IChatUserStateEventBus userStateEventBus,
            ICurrentChannelService currentChannelService,
            ChatInputBlockingService chatInputBlockingService,
            ChatSettingsAsset chatSettingsAsset,
            CommandRegistry commandRegistry,
            IChatHistory chatHistory,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ChatMemberListService chatMemberListService) : base(viewFactory)
        {
            this.chatConfig = chatConfig;
            this.eventBus = eventBus;
            this.currentChannelService = currentChannelService;
            this.chatInputBlockingService = chatInputBlockingService;
            this.chatSettingsAsset = chatSettingsAsset;
            this.commandRegistry = commandRegistry;
            this.chatHistory = chatHistory;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.chatMemberListService = chatMemberListService;
            
            chatUserStateBridge = new ChatUserStateBridge(userStateEventBus, eventBus, currentChannelService);
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        private CancellationTokenSource initCts;
        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            
            uiScope = new EventSubscriptionScope();
            
            viewInstance.OnPointerEnterEvent += HandlePointerEnter;
            viewInstance.OnPointerExitEvent += HandlePointerExit;

            chatMemberListService.Start();
            
            chatClickDetectionService = new ChatClickDetectionService(viewInstance.transform as RectTransform);
            chatClickDetectionService.Initialize(elementsToIgnore: new List<Transform>
            {
                viewInstance.TitlebarView.CloseChatButton.transform,
                viewInstance.TitlebarView.CloseMemberListButton.transform,
                viewInstance.TitlebarView.OpenMemberListButton.transform,
                viewInstance.TitlebarView.BackFromMemberList.transform,
                viewInstance.InputView.InputField.transform
            });

            var titleBarPresenter = new ChatTitlebarPresenter(viewInstance.TitlebarView,
                eventBus,
                chatMemberListService,
                commandRegistry.GetTitlebarViewModel);

            var channelListPresenter = new ChatChannelsPresenter(viewInstance.ConversationToolbarView2,
                eventBus,
                profileRepositoryWrapper,
                commandRegistry.SelectChannel,
                commandRegistry.LeaveChannel,
                commandRegistry.CreateChannelViewModel);

            var messageFeedPresenter = new ChatMessageFeedPresenter(viewInstance.MessageFeedView,
                eventBus,
                currentChannelService,
                commandRegistry.GetMessageHistory,
                commandRegistry.CreateMessageViewModel,
                commandRegistry.MarkChannelAsRead);

            var inputPresenter = new ChatInputPresenter(
                viewInstance.InputView,
                eventBus,
                currentChannelService,
                commandRegistry.GetUserChatStatus,
                commandRegistry.SendMessage);
            
            var memberListPresenter = new ChatMemberListPresenter(
                viewInstance.MemberListView,
                eventBus,
                chatMemberListService,
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
            //SetVisibility(true);
            
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
        }
    }
}