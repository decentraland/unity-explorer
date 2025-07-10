using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatStates;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using MVC;
using Prime31.StateKit;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatMainPresenter : ControllerBase<ChatMainView, ChatControllerShowParams>,
        IControllerInSharedSpace<ChatMainView, ChatControllerShowParams>
    {
        public event Action OnPointerEnter;
        public event Action OnPointerExit;
        public event Action OnClickInside;
        public event Action OnClickOutside;
        
        private readonly IChatHistory chatHistory;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly ChatUserStateEventBus chatUserStateEventBus;
        private readonly ObjectProxy<IFriendsService> friendsServiceProxy;
        private readonly ChatService chatService;
        private readonly ChatMemberListService chatMemberListService;
        private ChatClickDetectionService chatClickDetectionService;
        private ChatInputBlockingService chatInputBlockingService;
        
        private readonly IChatPresenterFactory presenterFactory;
        public ChatTitlebarPresenter? titleBarPresenter;
        public ChatChannelsPresenter? chatChannelsPresenter;
        public ChatMemberListPresenter? memberListPresenter;
        public ChatMessageFeedPresenter? messageViewerPresenter;
        public ChatInputPresenter? chatInputPresenter;
        
        private ChatConfig config;
        private SKStateMachine<ChatMainPresenter> fsm;
        
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;
        public ChatChannel.ChannelId CurrentChannelId { get; set; }
        
        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;
        public bool IsVisibleInSharedSpace => State != ControllerState.ViewHidden 
                                              && !IsHidden();

        public ChatMainPresenter(ViewFactoryMethod viewFactory,
            IChatPresenterFactory presenterFactory,
            IChatHistory chatHistory,
            IChatMessagesBus chatMessagesBus,
            ObjectProxy<IFriendsService> friendsServiceProxy,
            ChatService chatService,
            ChatMemberListService chatMemberListService,
            ChatInputBlockingService chatInputBlockingService) : base(viewFactory)
        {
            this.presenterFactory = presenterFactory;
            this.chatHistory = chatHistory;
            this.chatMessagesBus = chatMessagesBus;
            this.friendsServiceProxy = friendsServiceProxy;
            this.chatService = chatService;
            this.chatMemberListService = chatMemberListService;
            this.chatInputBlockingService = chatInputBlockingService;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            
            config = viewInstance?.Config;
            chatClickDetectionService = new ChatClickDetectionService(viewInstance.transform as RectTransform);
            
            chatChannelsPresenter = presenterFactory.CreateConversationList(viewInstance.ConversationToolbarView2,viewInstance.Config);
            memberListPresenter = presenterFactory.CreateMemberList(viewInstance.MemberListView);
            messageViewerPresenter = presenterFactory.CreateMessageFeed(viewInstance.MessageFeedView);
            chatInputPresenter = presenterFactory.CreateChatInput(viewInstance.InputView);
            titleBarPresenter = presenterFactory.CreateTitlebar(viewInstance.TitlebarView);
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            
            chatClickDetectionService.Initialize();
            chatInputBlockingService.Initialize();
            
            SubscribeToGlobalEvents();
            EnableChildPresenters();
            SetupFiniteStateMachine();
            
            chatService.InitializeAsync().Forget();
            chatMemberListService.Start();
        }

        protected override void OnViewClose()
        {
            chatClickDetectionService.Dispose();
            UnsubscribeFromGlobalEvents();
            Dispose();
        }

        private void EnableChildPresenters()
        {
            chatChannelsPresenter!.Activate();
            messageViewerPresenter!.Activate();
            chatInputPresenter!.Activate();
            titleBarPresenter!.Enable();
            memberListPresenter!.Deactivate();
        }
        
        private void SetupFiniteStateMachine()
        {
            fsm = new SKStateMachine<ChatMainPresenter>(this, new InitChatState(chatHistory));
            fsm.addState(new DefaultChatState());
            fsm.addState(new FocusedChatState());
            fsm.addState(new MembersChatState());
            fsm.addState(new MinimizedChatState());
            
            fsm.onStateChanged += OnFsmStateChanged;
            
            fsm.changeState<DefaultChatState>();
        }
        
        private bool IsHidden()
        {
            return fsm.currentState is MinimizedChatState;
        }

        public void BlockPlayerInput() => chatInputBlockingService.Block();
        public void UnblockPlayerInput() => chatInputBlockingService.Unblock();
        
        private void OnHistoryReadMessagesChanged(ChatChannel changedChannel)
        {
            
        }

        private void OnHistoryChannelRemoved(ChatChannel.ChannelId removedChannel)
        {
            
        }

        private void OnHistoryChannelAdded(ChatChannel addedChannel)
        {
            
        }
        
        private void OnFsmStateChanged()
        {
            ReportHub.Log(ReportCategory.UNSPECIFIED, $"On state changed: {fsm.currentState.ToString()}");
        }

        private void OnChatBusMessageAdded(ChatChannel.ChannelId arg1, ChatMessage arg2)
        {
            ReportHub.Log(ReportData.UNSPECIFIED, $"HandleBusMessageAdded: {arg1} - {arg2}");
        }
        
        private void HandleBusMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            ReportHub.Log(ReportData.UNSPECIFIED, $"HandleBusMessageAdded: {destinationChannel.Id} - {addedMessage.Message}");
        }
        
        private void HandleChatChannelSelected(ChatChannel.ChannelId channelId)
        {
            if (CurrentChannelId.Id == channelId.Id) return;
            
            if (chatHistory.Channels.ContainsKey(new ChatChannel.ChannelId(channelId.Id)))
            {
                var channel = chatHistory.Channels[new ChatChannel.ChannelId(channelId.Id)];
                titleBarPresenter!.UpdateForChannel(channel);
                messageViewerPresenter!.LoadChannel(channel);
                memberListPresenter!.LoadMembersForChannel(channel);
                chatInputPresenter!.UpdateStateForChannel(channel);
            }
        }

        private void HandleMemberListToggled(bool active)
        {
            fsm.changeState<MembersChatState>();
        }

        private void HandlePanelCloseChat()
        {
            fsm.changeState<MinimizedChatState>();
        }

        private void HandleMessageSubmitted(string message)
        {
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }


        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ChatControllerShowParams showParams)
        {
            if (State != ControllerState.ViewHidden)
            {
                if (showParams.Focus)
                    fsm.changeState<FocusedChatState>();
                else
                    fsm.changeState<DefaultChatState>();

                ViewShowingComplete?.Invoke(this);
            }

            await UniTask.CompletedTask;
        }
        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            fsm.changeState<MinimizedChatState>();
            await UniTask.CompletedTask;
        }
        
         #region Subscriptions
        
        private void SubscribeToGlobalEvents()
        {
            if (viewInstance == null) return;
            
            viewInstance.OnPointerEnterEvent += () => OnPointerEnter?.Invoke();
            viewInstance.OnPointerExitEvent += () => OnPointerExit?.Invoke();
            
            chatClickDetectionService.OnClickInside += () => OnClickInside?.Invoke();
            chatClickDetectionService.OnClickOutside += () => OnClickOutside?.Invoke();
            
            chatMessagesBus.MessageAdded += OnChatBusMessageAdded;
            chatHistory.MessageAdded += HandleBusMessageAdded;
            chatHistory.ChannelAdded += OnHistoryChannelAdded;
            chatHistory.ChannelRemoved += OnHistoryChannelRemoved;
            chatHistory.ReadMessagesChanged += OnHistoryReadMessagesChanged;
            
            if (chatInputPresenter != null)
            {
                chatInputPresenter!.OnMessageSubmitted += HandleMessageSubmitted;
            }
            
            if (titleBarPresenter != null)
            {
                titleBarPresenter.OnMemberListToggle += HandleMemberListToggled;
                titleBarPresenter.OnCloseChat += HandlePanelCloseChat;
            }

            if (chatChannelsPresenter != null) 
                chatChannelsPresenter.OnConversationSelected += HandleChatChannelSelected;
        }
        
        private void UnsubscribeFromGlobalEvents()
        {
            chatClickDetectionService.OnClickInside -= () => OnClickInside?.Invoke();
            chatClickDetectionService.OnClickOutside -= () => OnClickOutside?.Invoke();
            
            if (viewInstance != null)
            {
                viewInstance.OnPointerEnterEvent -= () => OnPointerEnter?.Invoke();
                viewInstance.OnPointerExitEvent -= () => OnPointerExit?.Invoke();    
            }

            if (chatMessagesBus != null) 
                chatMessagesBus.MessageAdded -= OnChatBusMessageAdded;
            
            if (chatHistory != null)
            {
                chatHistory.MessageAdded -= HandleBusMessageAdded;
                chatHistory.ChannelAdded -= OnHistoryChannelAdded;
                chatHistory.ChannelRemoved -= OnHistoryChannelRemoved;
                chatHistory.ReadMessagesChanged -= OnHistoryReadMessagesChanged;
            }

            if (chatInputPresenter != null)
            {
                chatInputPresenter!.OnMessageSubmitted -= HandleMessageSubmitted;
            }
            
            if (titleBarPresenter != null)
            {
                titleBarPresenter.OnMemberListToggle -= HandleMemberListToggled;
                titleBarPresenter.OnCloseChat -= HandlePanelCloseChat;
            }

            if (chatChannelsPresenter != null) 
                chatChannelsPresenter.OnConversationSelected -= HandleChatChannelSelected;
        }
        
        #endregion
        
        public void SetPanelsFocusState(bool isFocused, bool animate)
        {
            viewInstance.SetSharedBackgroundFocusState(isFocused, animate, config.PanelsFadeDuration, config.PanelsFadeEase);
            messageViewerPresenter?.SetFocusState(isFocused, animate, config.PanelsFadeDuration,config.PanelsFadeEase);
            chatChannelsPresenter?.SetFocusState(isFocused, animate, config.PanelsFadeDuration,config.PanelsFadeEase);
            titleBarPresenter?.SetFocusState(isFocused, animate, config.PanelsFadeDuration,config.PanelsFadeEase);
        }
    }
}