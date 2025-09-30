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

        public void Execute(string messageId, string originalText, CancellationToken ct)
        {
            translationService
                .TranslateManualAsync(messageId, originalText, ct)
                .Forget();
        }
    }
}
