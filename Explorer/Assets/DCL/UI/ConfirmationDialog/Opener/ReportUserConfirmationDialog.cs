using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using System.Threading;
using UnityEngine;

namespace DCL.UI.ConfirmationDialog.Opener
{
    public static class ReportUserConfirmationDialog
    {
        private const string TEXT_FORMAT = "You will be redirected to a web form to report {0}.";
        private const string SUB_TEXT = "Please fill up the form as detailed as possible, providing evidence of the infraction.";
        private const string CONFIRM_TEXT = "Report";
        private const string CANCEL_TEXT = "Cancel";

        public static async UniTask<bool> ShowAsync(
            IConfirmationDialogOpener confirmationDialogOpener,
            string userName,
            Sprite? reportSprite,
            string reportCategory,
            CancellationToken ct)
        {
            Result<ConfirmationResult> dialogResult = await confirmationDialogOpener
                .OpenConfirmationDialogAsync(
                    new ConfirmationDialogParameter(
                        string.Format(TEXT_FORMAT, userName),
                        CANCEL_TEXT,
                        CONFIRM_TEXT,
                        reportSprite,
                        false, false,
                        subText: SUB_TEXT),
                    ct)
                .SuppressToResultAsync(reportCategory);

            return !ct.IsCancellationRequested && dialogResult.Success && dialogResult.Value != ConfirmationResult.CANCEL;
        }
    }
}
