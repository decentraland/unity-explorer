using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Translation.Service;

namespace DCL.Translation.Commands
{
    public class TranslateMessageCommand
    {
        private readonly ITranslationService translationService;

        public TranslateMessageCommand(ITranslationService translationService)
        {
            this.translationService = translationService;
        }

        public void Execute(string messageId, string originalText)
        {
            translationService
                .TranslateManualAsync(messageId, originalText, CancellationToken.None)
                .Forget();
        }
    }
}