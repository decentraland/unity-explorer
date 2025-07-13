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
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Chat.Services;
using DCL.Settings.Settings;
using DCL.UI.Profiles.Helpers;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatMainController : ControllerBase<ChatMainView, ChatControllerShowParams>,
                                  IControllerInSharedSpace<ChatMainView, ChatControllerShowParams>
    {
        private readonly IEventBus eventBus;
        private readonly ChatInputBlockingService chatInputBlockingService;
        private readonly ChatSettingsAsset chatSettingsAsset;
        private readonly UseCaseFactory useCaseFactory;
        private readonly IChatHistory chatHistory;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ChatMemberListService chatMemberListService;
        private readonly ICurrentChannelService currentChannelService;
        private readonly ChatConfig chatConfig;
        private ChatFsmController? fsmController;
        private EventSubscriptionScope uiScope;
        
        private ChatClickDetectionService chatClickDetectionService;
        
        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public event Action? PointerEntered;
        public event Action? PointerExited;
        
        public bool IsVisibleInSharedSpace => 
            State != ControllerState.ViewHidden &&
            fsmController is { IsMinimized: false };

        public ChatMainController(ViewFactoryMethod viewFactory,
            ChatConfig chatConfig,
            IEventBus eventBus,
            ICurrentChannelService currentChannelService,
            ChatInputBlockingService chatInputBlockingService,
            ChatSettingsAsset chatSettingsAsset,
            UseCaseFactory useCaseFactory,
            IChatHistory chatHistory,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ChatMemberListService chatMemberListService) : base(viewFactory)
        {
            this.chatConfig = chatConfig;
            this.eventBus = eventBus;
            this.currentChannelService = currentChannelService;
            this.chatInputBlockingService = chatInputBlockingService;
            this.chatSettingsAsset = chatSettingsAsset;
            this.useCaseFactory = useCaseFactory;
            this.chatHistory = chatHistory;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.chatMemberListService = chatMemberListService;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        private CancellationTokenSource initCts;
        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            
            uiScope = new EventSubscriptionScope();
            
            viewInstance.OnPointerEnterEvent += HandlePointerEnter;
            viewInstance.OnPointerExitEvent += HandlePointerExit;
            
            chatClickDetectionService = new ChatClickDetectionService(viewInstance.transform as RectTransform);
            chatClickDetectionService.Initialize(new List<Transform>
            {
                viewInstance.TitlebarView.CloseChatButton.transform,
                viewInstance.TitlebarView.CloseMemberListButton.transform,
            });
            
            var titleBarPresenter = new ChatTitlebarPresenter(viewInstance.TitlebarView,
                eventBus, profileRepositoryWrapper);

            var channelListPresenter = new ChatChannelsPresenter(viewInstance.ConversationToolbarView2,
                eventBus,
                profileRepositoryWrapper,
                useCaseFactory.SelectChannel,
                useCaseFactory.LeaveChannel,
                useCaseFactory.CreateChannelViewModel);

            var messageFeedPresenter = new ChatMessageFeedPresenter(viewInstance.MessageFeedView,
                eventBus,
                currentChannelService,
                useCaseFactory.GetMessageHistory,
                useCaseFactory.CreateMessageViewModel,
                useCaseFactory.MarkChannelAsRead);

            var inputPresenter = new ChatInputPresenter(
                viewInstance.InputView,
                eventBus,
                useCaseFactory.GetUserChatStatus,
                useCaseFactory.SendMessage);
            
            var memberListPresenter = new ChatMemberListPresenter(
                viewInstance.MemberListView,
                eventBus,
                chatMemberListService,
                profileRepositoryWrapper);
            
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

            fsmController = new ChatFsmController(eventBus,
                mediator,
                chatInputBlockingService,
                chatClickDetectionService,
                this);
            
            uiScope.Add(fsmController);
        }

        protected override void OnViewShow()
        {
            initCts = new CancellationTokenSource();
            useCaseFactory.InitializeChat.ExecuteAsync(initCts.Token).Forget();
            fsmController?.OnViewShow();
        }

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ChatControllerShowParams showParams)
        {
            if (State != ControllerState.ViewHidden)
            {
                fsmController?.SetInitialState(showParams.Focus);
                ViewShowingComplete?.Invoke(this);
            }
            await UniTask.CompletedTask;
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            fsmController?.Minimize();
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