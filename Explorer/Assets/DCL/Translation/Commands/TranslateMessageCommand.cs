using Cysharp.Threading.Tasks;
using DCL.Translation.Service;
using System.Threading;

namespace DCL.Translation
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
