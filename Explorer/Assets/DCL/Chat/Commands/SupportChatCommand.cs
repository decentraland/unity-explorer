using Cysharp.Threading.Tasks;
using DCL.Browser;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Opens the Decentraland support page in the default web browser.
    ///
    /// Usage:
    ///     /support
    /// </summary>
    public class SupportChatCommand : IChatCommand
    {
        private const string RESPONSE = "Opening support page in your browser...";

        private readonly SupportRequestService supportRequestService;

        public string Command => "support";
        public string Description => "<b>/support</b>\n  Open the Decentraland support page";

        public SupportChatCommand(SupportRequestService supportRequestService)
        {
            this.supportRequestService = supportRequestService;
        }

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 0;

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            supportRequestService.OpenSupport();
            return UniTask.FromResult(RESPONSE);
        }
    }
}
