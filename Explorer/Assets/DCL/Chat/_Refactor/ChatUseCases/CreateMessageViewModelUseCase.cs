using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.UI.InputFieldFormatting;

namespace DCL.Chat.ChatUseCases
{
    public class CreateMessageViewModelUseCase
    {
        private readonly ITextFormatter hyperlinkFormatter;

        public CreateMessageViewModelUseCase(ITextFormatter hyperlinkFormatter)
        {
            this.hyperlinkFormatter = hyperlinkFormatter;
        }

        public ChatMessageViewModel Execute(ChatMessage message)
        {
            if (message.IsSeparator)
                return new ChatMessageViewModel { IsSeparator = true };

            return new ChatMessageViewModel
            {
                Message = hyperlinkFormatter.FormatText(message.Message),
                SenderValidatedName = message.SenderValidatedName,
                SenderWalletId = message.SenderWalletId,
                SenderWalletAddress = message.SenderWalletAddress,
                IsPaddingElement = message.IsPaddingElement,
                IsSentByOwnUser = message.IsSentByOwnUser,
                IsSystemMessage = message.IsSystemMessage,
                IsMention = message.IsMention,
                IsSeparator = message.IsSeparator,
            };
        }
    }
}