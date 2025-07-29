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
    private readonly ChatChannelsView view;
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

    private bool isInitialized;

    private CancellationTokenSource lifeCts;
    private EventSubscriptionScope scope = new();

    public ChatChannelsPresenter(ChatChannelsView view,
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
        this.chatHistory.ChannelRemoved += OnChannelRemoved;
        this.chatHistory.ReadMessagesChanged += OnReadMessagesChanged;
        this.chatHistory.MessageAdded += OnMessageAdded;
        this.chatEventBus.OpenPrivateConversationRequested += OnOpenConversationUsingUserId;
        this.chatUserStateEventBus.UserConnectionStateChanged += OnLiveUserConnectionStateChange;

        scope.Add(this.eventBus.Subscribe<ChatEvents.InitialChannelsLoadedEvent>(OnInitialChannelsLoaded));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelUpdatedEvent>(OnChannelUpdated));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelAddedEvent>(OnChannelAdded));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelLeftEvent>(OnChannelLeft));
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

    private void OnChannelRemoved(ChatChannel.ChannelId removedChannel, ChatChannel.ChatChannelType channelType)
    {
        if (!isInitialized) return;
        viewModels.Remove(removedChannel);
        view.RemoveConversation(removedChannel);
    }

    private void RemoveChannelFromView(ChatChannel.ChannelId removedChannel)
    {
        throw new NotImplementedException();
    }

    private void OnChannelAdded(ChatEvents.ChannelAddedEvent evt)
    {
        AddChannelToView(evt.Channel);
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
        var viewModel = createChannelViewModelCommand
            .CreateViewModelAndFetch(channel, lifeCts.Token);
        viewModels[viewModel.Id] = viewModel;
        view.AddConversation(viewModel);
    }

    private void OnMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage, int index)
    {
        if (destinationChannel.Id.Equals(currentChannelService.CurrentChannelId))
            return;

        if (chatHistory.Channels.TryGetValue(destinationChannel.Id, out var channel))
        {
            UpdateUnreadCount(channel);
        }

        if (destinationChannel.ChannelType != ChatChannel.ChatChannelType.NEARBY)
        {
            view.MoveChannelToTop(destinationChannel.Id);
        }
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
        chatHistory.ChannelRemoved -= OnChannelRemoved;
        chatHistory.MessageAdded -= OnMessageAdded;
        chatHistory.ReadMessagesChanged -= OnReadMessagesChanged;
        
        chatEventBus.OpenPrivateConversationRequested -= OnOpenConversationUsingUserId;
        chatUserStateEventBus.UserConnectionStateChanged -= OnLiveUserConnectionStateChange;

        scope.Dispose();
    }
}
