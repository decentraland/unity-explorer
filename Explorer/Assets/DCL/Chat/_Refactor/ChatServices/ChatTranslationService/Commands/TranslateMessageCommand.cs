using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Translation.Service;

namespace DCL.Translation.Commands
{
    public class TranslateMessageCommand
    {
        private readonly ITranslationService service;

        public TranslateMessageCommand(ITranslationService service)
        {
            this.service = service;
        }

        public void Execute(string messageId)
        {
            service.TranslateManualAsync(messageId, CancellationToken.None).Forget();
        }
    }
}