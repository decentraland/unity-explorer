using DCL.Chat;
using DCL.Chat.History;

public class InitChatState : ChatState
{
    private readonly IChatHistory chatHistory;
    
    public InitChatState(IChatHistory chatHistory)
    {
        this.chatHistory = chatHistory;
    }

    public override void begin()
    {

    }

    public override void end()
    {
        
    }
}
