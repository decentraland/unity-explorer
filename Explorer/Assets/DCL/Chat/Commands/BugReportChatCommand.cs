using Cysharp.Threading.Tasks;
using DCL.BugReporting.UI;
using MVC;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Opens the bug report form, optionally pre-filling the description.
    ///
    /// Usage:
    ///     /bug [description]
    /// </summary>
    public class BugReportChatCommand : IChatCommand
    {
        private const string RESPONSE = "Opening the bug report form...";

        private readonly IMVCManager mvcManager;

        public string Command => "bug";
        public string Description => "<b>/bug [description]</b>\n  Report a bug";

        public BugReportChatCommand(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public bool ValidateParameters(string[] parameters) =>
            true;

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            string? prefilledDescription = parameters.Length > 0 ? string.Join(' ', parameters) : null;
            mvcManager.ShowAsync(BugReportController.IssueCommand(new BugReportParams(prefilledDescription))).Forget();
            return UniTask.FromResult(RESPONSE);
        }
    }
}
