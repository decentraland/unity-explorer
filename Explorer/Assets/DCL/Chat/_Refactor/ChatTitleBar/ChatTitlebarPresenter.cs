using System;
using DCL.Chat;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using DG.Tweening;
using Utilities;

public class ChatTitlebarPresenter : IDisposable
{
    private readonly IChatTitlebarView view;
    private readonly IEventBus eventBus;
    private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
    private readonly ChatConfig chatConfig;
    private readonly EventSubscriptionScope scope = new();

    public ChatTitlebarPresenter(
        IChatTitlebarView view,
        IEventBus eventBus,
        ProfileRepositoryWrapper profileRepositoryWrapper,
        ChatConfig chatConfig)
    {
        this.view = view;
        this.eventBus = eventBus;
        this.profileRepositoryWrapper = profileRepositoryWrapper;
        this.chatConfig = chatConfig;
        
        view.Initialize();
        
        view.CloseChatButtonClicked += OnCloseButtonClicked;
        view.OnMemberListToggled += OnMemberListToggled;
        
        scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
    }

    private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
    {
        if (evt.Channel.ChannelType == ChatChannel.ChatChannelType.USER)
        {
            view.SetupProfileView(new Web3Address(evt.Channel.Id.Id), profileRepositoryWrapper);
        }
        else if(evt.Channel.ChannelType == ChatChannel.ChatChannelType.NEARBY)
        {
            view.SetNearbyChannelImage();
            view.SetChannelNameText(chatConfig.NearbyConversationName);
        }
        else
        {
            view.SetChannelNameText(evt.Channel.Id.Id);
        }
    }
    
    private void OnMemberListToggled(bool active)
    {
        eventBus.Publish(new ChatEvents.ToggleMembersEvent { IsVisible = active });
    }

    private void OnCloseButtonClicked()
    {
        eventBus.Publish(new ChatEvents.CloseChatEvent());
    }
    
    public void Show()
    {
        view.Show();
    }
    
    public void Hide()
    {
        view.Hide();
    }

    public void UpdateForChannel(ChatChannel channel)
    {
        view.SetChannelNameText(channel.Id.ToString());
        view.SetupProfileView(new Web3Address(channel.Id.Id), profileRepositoryWrapper);
    }

    public void Dispose()
    {
        view.CloseChatButtonClicked -= OnCloseButtonClicked;
        view.OnMemberListToggled -= OnMemberListToggled;
        scope.Dispose();
    }

    public void SetFocusState(bool isFocused, bool animate, float duration, Ease easing)
    {
        view.SetFocusedState(isFocused, animate, duration, easing);
    }

    public void SetMemberCount(int memberCount)
    {
        
    }

    public void ShowMembersView()
    {
        
    }
}