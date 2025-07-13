using System;
using DCL.Chat.ChatUseCases;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.UI.Profiles.Helpers;
using DG.Tweening;

public class ChatChannelsPresenter : IDisposable
{
    private readonly IChatChannelsView view;
    private readonly IEventBus eventBus;
    private readonly SelectChannelUseCase selectChannelUseCase;
    private readonly LeaveChannelUseCase leaveChannelUseCase;
    private readonly CreateChannelViewModelUseCase createChannelViewModelUseCase;
    
    private EventSubscriptionScope scope = new();

    public ChatChannelsPresenter(IChatChannelsView view,
        IEventBus eventBus,
        ProfileRepositoryWrapper profileRepositoryWrapper,
        SelectChannelUseCase selectChannelUseCase,
        LeaveChannelUseCase leaveChannelUseCase,
        CreateChannelViewModelUseCase createChannelViewModelUseCase)
    {
        this.view = view;
        this.view.Initialize(profileRepositoryWrapper);
        
        this.eventBus = eventBus;
        this.selectChannelUseCase = selectChannelUseCase;
        this.leaveChannelUseCase = leaveChannelUseCase;
        this.createChannelViewModelUseCase = createChannelViewModelUseCase;
        
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
        var initialModel = createChannelViewModelUseCase.CreateViewModelAndFetch(channel);
        view.AddConversation(initialModel);
    }
    
    private void OnViewConversationSelected(ChatChannel.ChannelId channelId)
    {
        selectChannelUseCase.Execute(channelId);
    }
    
    private void OnViewConversationRemovalRequested(ChatChannel.ChannelId channelId)
    {
        leaveChannelUseCase.Execute(channelId);
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