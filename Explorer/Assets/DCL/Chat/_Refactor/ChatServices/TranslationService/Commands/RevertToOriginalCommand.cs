using DCL.Translation.Service;

namespace DCL.Translation.Commands
{
    public class RevertToOriginalCommand
    {
        private readonly ITranslationService service;

        public RevertToOriginalCommand(ITranslationService service)
        {
            this.service = service;
        }

        public void Execute(string messageId)
        {
            service.RevertToOriginal(messageId);
        }
    }
}