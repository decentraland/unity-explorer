using DCL.Chat.MessageBus;
using DCL.Chat.Services;

namespace DCL.Chat.ChatUseCases
{
    public struct SendMessageCommandPayload
    {
        public string Body { get; set; }
    }
    
    public class SendMessageCommand
    {
        private const string ORIGIN = "chat";
        
        private readonly ICurrentChannelService currentChannelService;
        private readonly IChatMessagesBus chatMessageBus;
    
        public SendMessageCommand(
            IChatMessagesBus chatMessageBus,
            ICurrentChannelService currentChannelService)
        {
            this.currentChannelService = currentChannelService;
            this.chatMessageBus = chatMessageBus;
        }
    
        public void Execute(SendMessageCommandPayload commandPayload)
        {
            if (string.IsNullOrWhiteSpace(commandPayload.Body)) return;
    
            chatMessageBus.Send(
                currentChannelService.CurrentChannel,
                commandPayload.Body,
                ORIGIN);
        }
    }
}