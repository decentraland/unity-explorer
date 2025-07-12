using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatStates;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.Services;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Utilities;
using Prime31.StateKit;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatMainPresenter : IDisposable
    {
        public event Action OnCloseIntent;
        public event Action OnPointerEnter;
        public event Action OnPointerExit;
        public event Action OnClickInside;
        public event Action OnClickOutside;
        
        private readonly IChatHistory chatHistory;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IChatUserStateEventBus chatUserStateEventBus;
        private readonly ObjectProxy<IFriendsService> friendsServiceProxy;
        private readonly ChatService chatService;
        private readonly ChatMemberListService chatMemberListService;
        private readonly IChatPresenterFactory presenterFactory;
        
        private ChatClickDetectionService chatClickDetectionService;
        private ChatInputBlockingService chatInputBlockingService;
        
        public ChatTitlebarPresenter? titleBarPresenter;
        public ChatChannelsPresenter? chatChannelsPresenter;
        public ChatMemberListPresenter? memberListPresenter;
        public ChatMessageFeedPresenter? messageViewerPresenter;
        public ChatInputPresenter? chatInputPresenter;
        
        private ChatMainView viewInstance;
        private ChatConfig config;
        private SKStateMachine<ChatMainPresenter> fsm;
        
        public ChatChannel.ChannelId CurrentChannelId { get; set; }

        public ChatMainPresenter(IChatPresenterFactory presenterFactory,
            IChatHistory chatHistory,
            IChatMessagesBus chatMessagesBus,
            ObjectProxy<IFriendsService> friendsServiceProxy,
            ChatService chatService,
            ChatMemberListService chatMemberListService,
            ChatInputBlockingService chatInputBlockingService,
            IChatUserStateEventBus chatUserStateEventBus)
        {
            this.presenterFactory = presenterFactory;
            this.chatHistory = chatHistory;
            this.chatMessagesBus = chatMessagesBus;
            this.friendsServiceProxy = friendsServiceProxy;
            this.chatService = chatService;
            this.chatMemberListService = chatMemberListService;
            this.chatInputBlockingService = chatInputBlockingService;
            this.chatUserStateEventBus = chatUserStateEventBus;
        }
        
        public void SetView(ChatMainView? viewInstance)
        {
            this.viewInstance = viewInstance;
            config = viewInstance?.Config;
            chatClickDetectionService = new ChatClickDetectionService(viewInstance.transform as RectTransform);
            
            chatChannelsPresenter = presenterFactory.CreateConversationList(viewInstance.ConversationToolbarView2,viewInstance.Config);
            memberListPresenter = presenterFactory.CreateMemberList(viewInstance.MemberListView);
            messageViewerPresenter = presenterFactory.CreateMessageFeed(viewInstance.MessageFeedView);
            chatInputPresenter = presenterFactory.CreateChatInput(viewInstance.InputView);
            titleBarPresenter = presenterFactory.CreateTitlebar(viewInstance.TitlebarView);
        }

        public void OnViewShow()
        {
            chatClickDetectionService.Initialize(elementsToIgnore:new List<Transform>
            {
                viewInstance.TitlebarView.CurrentTitleBarCloseButton.transform
            });
            
            chatInputBlockingService.Initialize();
            
            SubscribeToGlobalEvents();
            EnableChildPresenters();
            SetupFiniteStateMachine();
            
            chatService.InitializeAsync().Forget();
            chatMemberListService.Start();
        }

        public void OnViewClose()
        {
            chatClickDetectionService.Dispose();
            chatMemberListService.Stop();
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
        
        public bool IsHidden()
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
            ReportHub.Log(ReportData.UNSPECIFIED, $"HandleBusMessageAdded: {arg1.Id} - {arg2.Message}");
        }
        
        private void OnChatMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            ReportHub.Log(ReportData.UNSPECIFIED, $"HandleBusMessageAdded: {destinationChannel.Id} - {addedMessage.Message}");
            messageViewerPresenter?.OnMessageReceived(destinationChannel, addedMessage);
        }
        
        private void HandleChatChannelSelected(ChatChannel.ChannelId channelId)
        {
            if (CurrentChannelId.Id == channelId.Id) return;
            
            if (chatHistory.Channels.ContainsKey(new ChatChannel.ChannelId(channelId.Id)))
            {
                var channel = chatHistory.Channels[new ChatChannel.ChannelId(channelId.Id)];
                titleBarPresenter!.UpdateForChannel(channel);
                memberListPresenter!.LoadMembersForChannel(channel);
                chatInputPresenter!.UpdateStateForChannel(channel);
                messageViewerPresenter!.LoadChannel(channel);
            }
        }

        private void HandleMemberListToggled(bool active)
        {
            fsm.changeState<MembersChatState>();
        }

        private void HandlePanelCloseChat()
        {
            fsm.changeState<MinimizedChatState>();
            OnCloseIntent?.Invoke();
        }

        private void HandleMessageSubmitted(string message)
        {
        }


        public void OnShown(ChatControllerShowParams showParams)
        {
            if (showParams.Focus)
                fsm.changeState<FocusedChatState>();
            else
                fsm.changeState<DefaultChatState>();
        }
        
        public void OnHidden()
        {
            fsm.changeState<MinimizedChatState>();
        }
        
        private void HandleMemberCountUpdated(int memberCount)
        {
            titleBarPresenter?.SetMemberCount(memberCount);
        }
        
         #region Subscriptions
        
        private void SubscribeToGlobalEvents()
        {
            if (viewInstance == null) return;
            
            chatMemberListService.OnMemberCountUpdated += HandleMemberCountUpdated;
            
            viewInstance.OnPointerEnterEvent += () => OnPointerEnter?.Invoke();
            viewInstance.OnPointerExitEvent += () => OnPointerExit?.Invoke();
            
            chatClickDetectionService.OnClickInside += () => OnClickInside?.Invoke();
            chatClickDetectionService.OnClickOutside += () => OnClickOutside?.Invoke();
            
            chatMessagesBus.MessageAdded += OnChatBusMessageAdded;
            chatHistory.MessageAdded += OnChatMessageAdded;
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
            chatMemberListService.OnMemberCountUpdated -= HandleMemberCountUpdated;
            
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
                chatHistory.MessageAdded -= OnChatMessageAdded;
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

        public void Dispose()
        {
            chatMessagesBus.Dispose();
            chatService.Dispose();
            chatMemberListService.Dispose();
            chatClickDetectionService.Dispose();
            titleBarPresenter?.Dispose();
            chatChannelsPresenter?.Dispose();
            memberListPresenter?.Dispose();
            messageViewerPresenter?.Dispose();
            chatInputPresenter?.Dispose();
        }
    }
}