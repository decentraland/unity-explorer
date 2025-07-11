using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Friends;
using DCL.Utilities;

public class ChatService : IDisposable
{
    public event Action<IReadOnlyCollection<string>> OnInitialized;
    
    private readonly ObjectProxy<IFriendsService> friendsServiceProxy;
    private readonly ChatHistoryStorage chatHistoryStorage;
    private readonly ChatUserStateUpdater chatUserStateUpdater;
    private readonly IChatHistory chatHistory;

    public ChatService(
        IChatHistory chatHistory,
        ObjectProxy<IFriendsService> friendsServiceProxy,
        ChatHistoryStorage? chatHistoryStorage,
        ChatUserStateUpdater? chatUserStateUpdater
    )
    {
        this.chatHistory = chatHistory;
        this.friendsServiceProxy = friendsServiceProxy;
        this.chatHistoryStorage = chatHistoryStorage;
        this.chatUserStateUpdater = chatUserStateUpdater;
    }

    public async UniTask InitializeAsync()
    {
        var nearbyChannel = chatHistory.AddOrGetChannel(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);
        chatHistory.AddMessage(nearbyChannel.Id, ChatMessage.NewFromSystem("Type /help for available commands."));

        for (int i = 0; i < 20; i++)
            chatHistory.AddMessage(nearbyChannel.Id, ChatMessage.NewFromSystem("Type /help for available commands."));
        
        if (!friendsServiceProxy.Configured)
            return;

        if (chatHistoryStorage != null)
            chatHistoryStorage?.LoadAllChannelsWithoutMessages();

        if (chatUserStateUpdater != null)
        {
            var connectedUsers = await chatUserStateUpdater.InitializeAsync(chatHistory.Channels.Keys);
            OnInitialized?.Invoke(connectedUsers);
        }
    }
    
    public void Dispose()
    {
        chatHistoryStorage?.Dispose();
        chatUserStateUpdater?.Dispose();
    }
}