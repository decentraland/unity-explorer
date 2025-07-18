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

        /// <summary>
        /// Opens a confirmation dialog with the provided parameters and returns the user's response.
        /// WARNING: This method overrides the ResultCallback in the dialogData parameter.
        /// </summary>
        public async UniTask<ConfirmationResult> OpenConfirmationDialogAsync(ConfirmationDialogParameter dialogData, CancellationToken ct)
        {
            ConfirmationResult result = ConfirmationResult.CANCEL;
            dialogData.ResultCallback = res => result = res;
            await mvcManager.ShowAsync(ConfirmationDialogController.IssueCommand(dialogData), ct);
            return result;
        }
    }
}
