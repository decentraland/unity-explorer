using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.UI.ConfirmationDialog.Opener
{
    public interface IConfirmationDialogOpener
    {
        UniTask<ConfirmationResult> OpenConfirmationDialogAsync(ConfirmationDialogParameter dialogData, CancellationToken ct);
    }
}
