using System;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.UI.InputFieldFormatting;
using DCL.Web3.Identities;
using DCL.Chat;

public class ChatMessageFeedPresenter : IDisposable
{
    private readonly IChatMessageFeedView view;
    private readonly IChatHistory chatHistory;
    private readonly IProfileCache profileCache;
    private readonly IWeb3IdentityCache web3IdentityCache;

    public ChatMessageFeedPresenter(
        IChatMessageFeedView view,
        IChatHistory chatHistory,
        IProfileCache profileCache,
        IWeb3IdentityCache web3IdentityCache)
    {
        this.view = view;
        this.chatHistory = chatHistory;
        this.profileCache = profileCache;
        this.web3IdentityCache = web3IdentityCache;
    }

    public void Enable()
    {
        // This presenter might not need to subscribe to anything globally
        // as the coordinator will push messages to it via OnMessageReceived.
    }

    public void LoadChannel(ChatChannel channel)
    {
        // Logic to convert channel.Messages to a list of MessageData and call view.SetMessages()
    }

    public void OnMessageReceived(ChatMessage message)
    {
        // Logic to convert the new ChatMessage to MessageData and call view.AppendMessage()
    }

    public void Dispose()
    {
        // Unsubscribe from any events if you add them later
    }
}