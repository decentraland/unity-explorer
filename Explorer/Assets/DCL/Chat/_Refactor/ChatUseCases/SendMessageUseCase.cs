using DCL.Chat.MessageBus;
using DCL.Chat.Services;

namespace DCL.Chat.ChatUseCases
{
    public struct SendMessageCommand
    {
        public string Body { get; set; }
    }
    
    public class SendMessageUseCase
    {
        private const string ORIGIN = "chat";
        
        private readonly ICurrentChannelService currentChannelService;
        private readonly IChatMessagesBus chatMessageBus;
    
        public SendMessageUseCase(
            IChatMessagesBus chatMessageBus,
            ICurrentChannelService currentChannelService)
        {
            this.currentChannelService = currentChannelService;
            this.chatMessageBus = chatMessageBus;
        }
    
        public void Execute(SendMessageCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.Body)) return;
    
            chatMessageBus.Send(
                currentChannelService.CurrentChannel,
                command.Body,
                ORIGIN);
        }
    }
}