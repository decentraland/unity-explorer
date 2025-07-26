using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Chat.ChatUseCases;
using DCL.Chat.ChatViewModels.ChannelViewModels;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.UI.Profiles.Helpers;
using DG.Tweening;

using Utility;

public class ChatChannelsPresenter : IDisposable
{
    private readonly IChatChannelsView view;
    private readonly IEventBus eventBus;
    private readonly IChatEventBus chatEventBus;
    private readonly IChatHistory chatHistory;
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
        IChatEventBus chatEventBus,
        IChatHistory chatHistory,
        ProfileRepositoryWrapper profileRepositoryWrapper,
        SelectChannelCommand selectChannelCommand,
        LeaveChannelCommand leaveChannelCommand,
        OpenPrivateConversationCommand openPrivateConversationCommand,
        CreateChannelViewModelCommand createChannelViewModelCommand)
    {
        this.view = view;
        this.view.Initialize(profileRepositoryWrapper);

        this.eventBus = eventBus;
        this.chatEventBus = chatEventBus;
        this.selectChannelCommand = selectChannelCommand;
        this.leaveChannelCommand = leaveChannelCommand;
        this.openPrivateConversationCommand = openPrivateConversationCommand;
        this.createChannelViewModelCommand = createChannelViewModelCommand;

        lifeCts = new CancellationTokenSource();
        
        view.ConversationSelected += OnViewConversationSelected;
        view.ConversationRemovalRequested += OnViewConversationRemovalRequested;

        chatHistory.ChannelAdded += OnRuntimeChannelAdded;
        chatEventBus.OpenPrivateConversationRequested += OnOpenConversationUsingUserId;

        scope.Add(this.eventBus.Subscribe<ChatEvents.InitialChannelsLoadedEvent>(OnInitialChannelsLoaded));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelUpdatedEvent>(OnChannelUpdated));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelAddedEvent>(OnChannelAdded));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelLeftEvent>(OnChannelLeft));
        scope.Add(this.eventBus.Subscribe<ChatEvents.UnreadMessagesUpdatedEvent>(OnUnreadMessagesUpdated));
        scope.Add(this.eventBus.Subscribe<ChatEvents.UserStatusUpdatedEvent>(OnUserStatusUpdated));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnSystemChannelSelected));
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

    private void OnChannelLeft(ChatEvents.ChannelLeftEvent evt)
    {
        viewModels.Remove(evt.Channel.Id);
        view.RemoveConversation(evt.Channel);
    }

    private void OnUnreadMessagesUpdated(ChatEvents.UnreadMessagesUpdatedEvent evt)
    {
        view.SetUnreadMessages(evt.ChannelId.Id, evt.Count);
    }

    private void OnUserStatusUpdated(ChatEvents.UserStatusUpdatedEvent evt)
    {
        view.SetOnlineStatus(evt.UserId, evt.IsOnline);
    }

    private void OnSystemChannelSelected(ChatEvents.ChannelSelectedEvent evt)
    {
        view.SelectConversation(evt.Channel.Id);
    }

    private void AddChannelToView(ChatChannel channel)
    {
        var viewModel = createChannelViewModelCommand.CreateViewModelAndFetch(channel, lifeCts.Token);
        viewModels[viewModel.Id] = viewModel;
        view.AddConversation(viewModel);
    }

    private void OnViewConversationSelected(ChatChannel.ChannelId channelId)
    {
        selectChannelCommand.Execute(channelId);
    }

    private void OnViewConversationRemovalRequested(ChatChannel.ChannelId channelId)
    {
        leaveChannelCommand.Execute(channelId);
    }

    public void Dispose()
    {
        chatHistory.ChannelAdded -= OnRuntimeChannelAdded;
        lifeCts.SafeCancelAndDispose();
        
        chatEventBus.OpenPrivateConversationRequested -= OnOpenConversationUsingUserId;
        view.ConversationSelected -= OnViewConversationSelected;
        view.ConversationRemovalRequested -= OnViewConversationRemovalRequested;
        scope.Dispose();
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
        view.SetFocusedState(isFocused, animate, duration,easing);
    }
}
