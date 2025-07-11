using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DG.Tweening;

public class ChatChannelsPresenter : IDisposable
{
    private readonly IChatChannelsView view;
    private readonly IChatHistory chatHistory;
    private readonly IChatUserStateEventBus chatUserStateEventBus;
    private readonly IProfileCache profileCache;
    private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
    private readonly ChatService chatService;
    private readonly ChannelItemFactory itemFactory;
    
    public event Action<ChatChannel.ChannelId>? OnConversationSelected;
    public event Action<ChatChannel.ChannelId>? OnConversationRemoved;

    public ChatChannelsPresenter(IChatChannelsView view,
        IChatHistory chatHistory,
        IChatUserStateEventBus chatUserStateEventBus,
        IProfileCache profileCache,
        ProfileRepositoryWrapper profileRepositoryWrapper,
        ChatService chatService,
        ChatConfig config)
    {
        this.view = view;
        this.chatHistory = chatHistory;
        this.chatUserStateEventBus = chatUserStateEventBus;
        this.profileCache = profileCache;
        this.profileRepositoryWrapper = profileRepositoryWrapper;
        this.chatService = chatService;

        itemFactory = new ChannelItemFactory(config,profileRepositoryWrapper);

        view.ConversationSelected += HandleChannelSelected;
        view.ConversationRemovalRequested += HandleChannelRemoved;
    }

    public void Activate()
    {
        chatService.OnInitialized += HandleChatServiceInitialized;
        chatHistory.ChannelAdded += HandleChannelAdded;
        chatHistory.ChannelRemoved += HandleChannelRemoved;
        chatHistory.ReadMessagesChanged += HandleUnreadMessagesChanged;
        chatUserStateEventBus.UserConnectionStateChanged += HandleUserConnectionStateChanged;
        
        foreach(var channel in chatHistory.Channels.Values)
        {
            HandleChannelAdded(channel);
        }
    }

    private void HandleChatServiceInitialized(IReadOnlyCollection<string> connectedUsers)
    {
        foreach (var channel in chatHistory.Channels.Values)
        {
            if (channel.ChannelType == ChatChannel.ChatChannelType.USER)
            {
                view.SetOnlineStatus(channel.Id.Id, connectedUsers.Contains(channel.Id.Id));
            }
        }
    }

    private void HandleChannelSelected(ChatChannel.ChannelId channelId)
    {
        OnConversationSelected?.Invoke(channelId);
    }
    
    private void HandleChannelRemoved(ChatChannel.ChannelId channelId)
    {
        OnConversationRemoved?.Invoke(channelId);
    }
    
    private void HandleUserConnectionStateChanged(string userId, bool isConnected)
    {
        view.SetOnlineStatus(userId, isConnected);
    }

    private void HandleUnreadMessagesChanged(ChatChannel changedChannel)
    {
    }

    private void HandleChannelAdded(ChatChannel channel)
    {
        ReportHub.Log(ReportCategory.UNSPECIFIED, "HandleChannelAdded: " + channel.Id);
        CreateAndAddItemAsync(channel).Forget();
    }
    
    private async UniTaskVoid CreateAndAddItemAsync(ChatChannel channel)
    {
        ChatConversationsToolbarViewItem newItem = await itemFactory.Create(channel, view.ItemsContainer);
        view.AddItem(newItem);
    }
    
    private void HandleViewConversationRemoved(string channelId)
    {
        // Tell the data layer to remove the channel. The view will update automatically
        // when the chatHistory.ChannelRemoved event fires.
        chatHistory.RemoveChannel(new ChatChannel.ChannelId(channelId));
    }

    public void Dispose()
    {
        chatService.OnInitialized -= HandleChatServiceInitialized;
        view.ConversationSelected -= HandleChannelSelected;
        view.ConversationRemovalRequested -= HandleChannelRemoved;
        chatHistory.ChannelAdded -= HandleChannelAdded;
        chatHistory.ChannelRemoved -= HandleChannelRemoved;
        chatHistory.ReadMessagesChanged -= HandleUnreadMessagesChanged;
        chatUserStateEventBus.UserConnectionStateChanged -= HandleUserConnectionStateChanged;
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