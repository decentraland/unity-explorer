using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.UI.ConfirmationDialog.Opener
{
    public interface IConfirmationDialogOpener
    {
        /// <summary>
        /// Opens a confirmation dialog with the provided parameters and returns the user's response.
        /// WARNING: This method overrides the ResultCallback in the dialogData parameter.
        /// </summary>
        UniTask<ConfirmationResult> OpenConfirmationDialogAsync(ConfirmationDialogParameter dialogData, CancellationToken ct);
    }
}
