using DCL.Chat;
using DCL.Chat.History;
using DCL.Diagnostics;

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
