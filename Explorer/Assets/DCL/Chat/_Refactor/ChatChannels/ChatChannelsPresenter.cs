using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatUseCases;
using DCL.Chat.ChatViewModels.ChannelViewModels;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.Services;
using DCL.UI.Profiles.Helpers;
using DG.Tweening;

using Utility;

public class ChatChannelsPresenter : IDisposable
{
    private readonly IChatChannelsView view;
    private readonly IEventBus eventBus;
    private readonly IChatEventBus chatEventBus;
    private readonly IChatMessagesBus chatMessageBus;
    private readonly IChatUserStateEventBus chatUserStateEventBus;
    private readonly IChatHistory chatHistory;
    private readonly ICurrentChannelService currentChannelService;
    private readonly SelectChannelCommand selectChannelCommand;
    private readonly LeaveChannelCommand leaveChannelCommand;
    private readonly OpenPrivateConversationCommand openPrivateConversationCommand;
    private readonly CreateChannelViewModelCommand createChannelViewModelCommand;
    private readonly Dictionary<ChatChannel.ChannelId, BaseChannelViewModel> viewModels = new();

    private bool isInitialized  ;

    private CancellationTokenSource lifeCts;
    private EventSubscriptionScope scope = new();
    
    public ChatChannelsPresenter(IChatChannelsView view,
        IEventBus eventBus,
        IChatMessagesBus chatMessageBus,
        IChatEventBus chatEventBus,
        IChatUserStateEventBus chatUserStateEventBus,
        IChatHistory chatHistory,
        ICurrentChannelService currentChannelService,
        ProfileRepositoryWrapper profileRepositoryWrapper,
        SelectChannelCommand selectChannelCommand,
        LeaveChannelCommand leaveChannelCommand,
        OpenPrivateConversationCommand openPrivateConversationCommand,
        CreateChannelViewModelCommand createChannelViewModelCommand)
    {
        this.view = view;
        this.view.Initialize(profileRepositoryWrapper);

        this.eventBus = eventBus;
        this.chatMessageBus = chatMessageBus;
        this.chatEventBus = chatEventBus;
        this.chatHistory = chatHistory;
        this.chatUserStateEventBus = chatUserStateEventBus;
        this.currentChannelService = currentChannelService;
        this.selectChannelCommand = selectChannelCommand;
        this.leaveChannelCommand = leaveChannelCommand;
        this.openPrivateConversationCommand = openPrivateConversationCommand;
        this.createChannelViewModelCommand = createChannelViewModelCommand;

        lifeCts = new CancellationTokenSource();
        
        view.ConversationSelected += OnViewConversationSelected;
        view.ConversationRemovalRequested += OnViewConversationRemovalRequested;

        this.chatHistory.ChannelAdded += OnRuntimeChannelAdded;
        this.chatHistory.ReadMessagesChanged += OnReadMessagesChanged;
        
        this.chatEventBus.OpenPrivateConversationRequested += OnOpenConversationUsingUserId;
        this.chatMessageBus.MessageAdded += OnMessageAdded;
        this.chatUserStateEventBus.UserConnectionStateChanged += OnLiveUserConnectionStateChange;
        
        scope.Add(this.eventBus.Subscribe<ChatEvents.InitialChannelsLoadedEvent>(OnInitialChannelsLoaded));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelUpdatedEvent>(OnChannelUpdated));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelAddedEvent>(OnChannelAdded));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelLeftEvent>(OnChannelLeft));
        scope.Add(this.eventBus.Subscribe<ChatEvents.MessageReceivedEvent>(OnMessageReceived));
        scope.Add(this.eventBus.Subscribe<ChatEvents.UnreadMessagesUpdatedEvent>(OnUnreadMessagesUpdated));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnSystemChannelSelected));
    }

    private async void OnLiveUserConnectionStateChange(string userId, bool isConnected)
    {
        await UniTask.SwitchToMainThread();
        
        var channelId = new ChatChannel.ChannelId(userId);

        if (viewModels.TryGetValue(channelId, out var baseVm) &&
            baseVm is UserChannelViewModel userVm)
        {
            if (userVm.IsOnline != isConnected)
            {
                userVm.IsOnline = isConnected;
                view.UpdateConversation(userVm);
            }
        }
    }

    private void OnOpenConversationUsingUserId(string userId)
    {
        openPrivateConversationCommand.Execute(userId);
    }


    private void OnChannelUpdated(ChatEvents.ChannelUpdatedEvent evt)
    {
        if (viewModels.TryGetValue(evt.ViewModel.Id, out _))
        {
            viewModels[evt.ViewModel.Id] = evt.ViewModel;
            view.UpdateConversation(evt.ViewModel);
        }
    }

    private void OnInitialChannelsLoaded(ChatEvents.InitialChannelsLoadedEvent evt)
    {
        if (isInitialized) return;

        lifeCts.Cancel();
        lifeCts.Dispose();
        lifeCts = new CancellationTokenSource();
        
        view.Clear();
        viewModels.Clear();

        foreach (var channel in evt.Channels)
        {
            AddChannelToView(channel);
        }

        isInitialized = true;
    }

    private void OnRuntimeChannelAdded(ChatChannel channel)
    {
        if (!isInitialized) return;
        AddChannelToView(channel);
    }

    private void OnChannelAdded(ChatEvents.ChannelAddedEvent evt)
    {
        AddChannelToView(evt.Channel);
    }

    private void OnUnreadMessagesUpdated(ChatEvents.UnreadMessagesUpdatedEvent evt)
    {
        view.SetUnreadMessages(evt.ChannelId, evt.Count);
    }

    private void OnSystemChannelSelected(ChatEvents.ChannelSelectedEvent evt)
    {
        view.SelectConversation(evt.Channel.Id);
    }

    private void OnViewConversationSelected(ChatChannel.ChannelId channelId)
    {
        selectChannelCommand.Execute(channelId);
    }

    private void OnViewConversationRemovalRequested(ChatChannel.ChannelId channelId)
    {
        leaveChannelCommand.Execute(channelId);
    }

    private void OnChannelLeft(ChatEvents.ChannelLeftEvent evt)
    {
        viewModels.Remove(evt.Channel.Id);
        view.RemoveConversation(evt.Channel);
    }

    private void AddChannelToView(ChatChannel channel)
    {
        var viewModel = createChannelViewModelCommand.CreateViewModelAndFetch(channel, lifeCts.Token);
        viewModels[viewModel.Id] = viewModel;
        view.AddConversation(viewModel);
    }

    private void OnMessageAdded(ChatChannel.ChannelId channelId,
        ChatChannel.ChatChannelType channelType, ChatMessage message)
    {
        if (channelId.Equals(currentChannelService.CurrentChannelId))
            return;

        if (chatHistory.Channels.TryGetValue(channelId, out var channel))
        {
            UpdateUnreadCount(channel);
        }
    }

    private void OnMessageReceived(ChatEvents.MessageReceivedEvent evt)
    {
        if (evt.ChannelId.Equals(currentChannelService.CurrentChannelId))
            return;

        UpdateUnreadCount(currentChannelService.CurrentChannel);
    }

    private void OnReadMessagesChanged(ChatChannel changedChannel)
    {
        // TODO: check if we are at the bottom of the channel
        // TODO: check if the channel is the current one?
        UpdateUnreadCount(changedChannel);
    }

    private void UpdateUnreadCount(ChatChannel channel)
    {
        int unreadCount = channel.Messages.Count - channel.ReadMessages;
        view.SetUnreadMessages(channel.Id, unreadCount);
    }

    public void Show()
    {
        view.Show();
    }

    public void Hide()
    {
        view.Hide();
    }

    public void SetFocusState(bool isFocused, bool animate, float duration, Ease easing)
    {
        view.SetFocusedState(isFocused, animate, duration, easing);
    }

    public void Dispose()
    {
        lifeCts.SafeCancelAndDispose();

        view.ConversationSelected -= OnViewConversationSelected;
        view.ConversationRemovalRequested -= OnViewConversationRemovalRequested;

        chatHistory.ChannelAdded -= OnRuntimeChannelAdded;
        chatMessageBus.MessageAdded -= OnMessageAdded;
        chatHistory.ReadMessagesChanged -= OnReadMessagesChanged;
        
        chatEventBus.OpenPrivateConversationRequested -= OnOpenConversationUsingUserId;
        chatUserStateEventBus.UserConnectionStateChanged -= OnLiveUserConnectionStateChange;

        scope.Dispose();
    }
}