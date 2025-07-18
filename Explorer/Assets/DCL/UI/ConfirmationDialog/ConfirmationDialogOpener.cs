using Cysharp.Threading.Tasks;
using DCL.UI.ConfirmationDialog.Opener;
using MVC;
using System.Threading;

namespace DCL.UI.ConfirmationDialog
{
    public class ConfirmationDialogOpener : IConfirmationDialogOpener
    {
        private readonly IMVCManager mvcManager;

        public ConfirmationDialogOpener(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public UniTask OpenConfirmationDialogAsync(ConfirmationDialogParameter dialogData, CancellationToken ct) =>
            mvcManager.ShowAsync(ConfirmationDialogController.IssueCommand(dialogData), ct);
    }
}
