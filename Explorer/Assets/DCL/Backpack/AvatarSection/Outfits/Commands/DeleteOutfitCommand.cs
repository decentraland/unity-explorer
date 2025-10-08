using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Diagnostics;
using DCL.UI.ConfirmationDialog.Opener;
using MVC;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public enum DeleteOutfitOutcome
    {
        Success,
        Cancelled,
        Failed
    }

    /// <summary>
    ///     Owns the confirmation dialog construction and the delete action.
    ///     Caller decides UI state transitions based on the outcome.
    /// </summary>
    public sealed class DeleteOutfitCommand
    {
        private const string OUTFIT_POPUP_DELETE_TEXT = "Are you sure you want to delete this Outfit?";
        private const string OUTFIT_POPUP_DELETE_CANCEL_TEXT = "CANCEL";
        private const string OUTFIT_POPUP_DELETE_CONFIRM_TEXT = "YES";

        private readonly IOutfitsService outfitsService;

        public DeleteOutfitCommand(IOutfitsService outfitsService)
        {
            this.outfitsService = outfitsService;
        }

        public async UniTask<DeleteOutfitOutcome> ExecuteAsync(
            int slotIndex,
            CancellationToken ct,
            Action? onConfirmed = null)
        {
            ConfirmationResult decision;
            try
            {
                var dialogParams = BuildDialogParams();
                decision = await ViewDependencies
                    .ConfirmationDialogOpener
                    .OpenConfirmationDialogAsync(dialogParams, ct);
            }
            catch (OperationCanceledException)
            {
                return DeleteOutfitOutcome.Cancelled;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                return DeleteOutfitOutcome.Failed;
            }

            if (ct.IsCancellationRequested || decision == ConfirmationResult.CANCEL)
                return DeleteOutfitOutcome.Cancelled;

            onConfirmed?.Invoke();

            try
            {
                await outfitsService.DeleteOutfitAsync(slotIndex, ct);
                return DeleteOutfitOutcome.Success;
            }
            catch (OperationCanceledException)
            {
                return DeleteOutfitOutcome.Cancelled;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                return DeleteOutfitOutcome.Failed;
            }
        }

        private ConfirmationDialogParameter BuildDialogParams()
        {
            return new ConfirmationDialogParameter(OUTFIT_POPUP_DELETE_TEXT,
                OUTFIT_POPUP_DELETE_CANCEL_TEXT,
                OUTFIT_POPUP_DELETE_CONFIRM_TEXT, null,
                false,
                false);
        }
    }
}