using System;
using DCL.Chat.ChatUseCases;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.UI.Profiles.Helpers;
using DG.Tweening;
using Utilities;

public class ChatChannelsPresenter : IDisposable
{
    private readonly IChatChannelsView view;
    private readonly IEventBus eventBus;
    private readonly SelectChannelCommand _selectChannelCommand;
    private readonly LeaveChannelCommand _leaveChannelCommand;
    private readonly CreateChannelViewModelCommand _createChannelViewModelCommand;
    
    private EventSubscriptionScope scope = new();

    public ChatChannelsPresenter(IChatChannelsView view,
        IEventBus eventBus,
        ProfileRepositoryWrapper profileRepositoryWrapper,
        SelectChannelCommand selectChannelCommand,
        LeaveChannelCommand leaveChannelCommand,
        CreateChannelViewModelCommand createChannelViewModelCommand)
    {
        this.view = view;
        this.view.Initialize(profileRepositoryWrapper);
        
        this.eventBus = eventBus;
        this._selectChannelCommand = selectChannelCommand;
        this._leaveChannelCommand = leaveChannelCommand;
        this._createChannelViewModelCommand = createChannelViewModelCommand;
        
        view.ConversationSelected += OnViewConversationSelected;
        view.ConversationRemovalRequested += OnViewConversationRemovalRequested;
        
        scope.Add(this.eventBus.Subscribe<ChatEvents.InitialChannelsLoadedEvent>(OnInitialChannelsLoaded));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelUpdatedEvent>(OnChannelUpdated));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelAddedEvent>(OnChannelAdded));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelLeftEvent>(OnChannelLeft));
        scope.Add(this.eventBus.Subscribe<ChatEvents.UnreadMessagesUpdatedEvent>(OnUnreadMessagesUpdated));
        scope.Add(this.eventBus.Subscribe<ChatEvents.UserStatusUpdatedEvent>(OnUserStatusUpdated));
        scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnSystemChannelSelected));
    }

    private void OnChannelUpdated(ChatEvents.ChannelUpdatedEvent evt)
    {
        view.UpdateConversation(evt.ViewModel);
    }

    private void OnInitialChannelsLoaded(ChatEvents.InitialChannelsLoadedEvent evt)
    {
        view.Clear();
        foreach (var channel in evt.Channels)
        {
            AddChannelToView(channel);
        }
    }

    private void OnChannelAdded(ChatEvents.ChannelAddedEvent evt)
    {
        AddChannelToView(evt.Channel);
    }

    private void OnChannelLeft(ChatEvents.ChannelLeftEvent evt)
    {
        view.RemoveConversation(evt.ChannelId.Id);
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
        var initialModel = _createChannelViewModelCommand.CreateViewModelAndFetch(channel);
        view.AddConversation(initialModel);
    }
    
    private void OnViewConversationSelected(ChatChannel.ChannelId channelId)
    {
        _selectChannelCommand.Execute(channelId);
    }
    
    private void OnViewConversationRemovalRequested(ChatChannel.ChannelId channelId)
    {
        _leaveChannelCommand.Execute(channelId);
    }
    
    public void Dispose()
    {
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