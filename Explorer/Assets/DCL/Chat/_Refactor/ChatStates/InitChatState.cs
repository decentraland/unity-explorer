using DCL.Chat;
using DCL.Chat.History;
using DCL.Diagnostics;

/// <summary>
/// Purpose: One-time setup of initial data.
/// begin():
///     Create the "Nearby" channel in chatHistory.
///     Add the welcome message to it.
///     Set the presenter's CurrentChannelId to "Nearby".
/// </summary>
public class InitChatState : ChatState
{
    private readonly IChatHistory chatHistory;
    
    public InitChatState(IChatHistory chatHistory)
    {
        this.chatHistory = chatHistory;
    }

    public override void begin()
    {
        var nearbyChannel = chatHistory.AddOrGetChannel(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);
        chatHistory.AddMessage(nearbyChannel.Id, ChatMessage.NewFromSystem("Type /help for available commands."));
        nearbyChannel.MarkAllMessagesAsRead();
        _context.CurrentChannelId = nearbyChannel.Id;
    }

    public override void end()
    {
        
    }
}
