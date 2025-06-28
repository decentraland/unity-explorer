using System;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;

public class ChatConversationToolbarPresenter : IDisposable
{
    private readonly IChatConversationToolbarView view;
    private readonly IChatHistory chatHistory;
    private readonly IChatUserStateEventBus chatUserStateEventBus;
    private IProfileCache profileCache;
    private ProfileRepositoryWrapper profileRepositoryWrapper;

    public event Action<string>? OnConversationSelected;
    public event Action<string>? OnConversationRemoved;

    public ChatConversationToolbarPresenter(IChatConversationToolbarView view,
        IChatHistory chatHistory,
        IChatUserStateEventBus chatUserStateEventBus,
        IProfileCache profileCache,
        ProfileRepositoryWrapper profileRepositoryWrapper)
    {
        this.view = view;
        this.chatHistory = chatHistory;
        this.chatUserStateEventBus = chatUserStateEventBus;
        this.profileCache = profileCache;
        this.profileRepositoryWrapper = profileRepositoryWrapper;

        view.OnConversationSelected += HandleConversationSelected;
        view.OnConversationRemoved += HandleChannelRemoved;
    }

    private void HandleChannelRemoved(string channelId)
    {
        OnConversationRemoved?.Invoke(channelId);
    }

    private void HandleConversationSelected(string channelId)
    {
        OnConversationSelected?.Invoke(channelId);
    }

    public void Enable()
    {
        chatHistory.ChannelAdded += HandleChannelAdded;
        chatHistory.ChannelRemoved += HandleChannelRemoved;
        chatHistory.ReadMessagesChanged += HandleUnreadMessagesChanged;
        chatUserStateEventBus.UserConnectionStateChanged += HandleUserConnectionStateChanged;
    }

    private void HandleUserConnectionStateChanged(string userId, bool isConnected)
    {
    }

    private void HandleUnreadMessagesChanged(ChatChannel changedChannel)
    {
    }

    private void HandleChannelRemoved(ChatChannel.ChannelId removedChannel)
    {
    }

    private void HandleChannelAdded(ChatChannel addedChannel)
    {
    }

    public void Dispose()
    {
        view.OnConversationSelected -= HandleConversationSelected;
        view.OnConversationRemoved -= HandleChannelRemoved;
        chatHistory.ChannelAdded -= HandleChannelAdded;
        chatHistory.ChannelRemoved -= HandleChannelRemoved;
        chatHistory.ReadMessagesChanged -= HandleUnreadMessagesChanged;
        chatUserStateEventBus.UserConnectionStateChanged -= HandleUserConnectionStateChanged;
    }
}