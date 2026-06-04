using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.AvatarSection.Outfits.Repository;
using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Diagnostics;
using DCL.UI.ConfirmationDialog.Opener;
using MVC;
using UnityEngine;

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

        private readonly OutfitsRepository outfitsRepository;
        private readonly IAvatarScreenshotService screenshotService;
        private readonly Sprite deleteIcon;

        public DeleteOutfitCommand(OutfitsRepository outfitsRepository,
            IAvatarScreenshotService screenshotService,
            Sprite deleteIcon)
        {
            this.outfitsRepository = outfitsRepository;
            this.screenshotService = screenshotService;
            this.deleteIcon = deleteIcon;
        }

        public async UniTask<DeleteOutfitOutcome> ExecuteAsync(int slotIndex, CancellationToken ct)
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

            try
            {
                await outfitsRepository.DeleteSlotAsync(slotIndex, ct);

                // Once DeleteSlotAsync returns we know the publish completed (it's
                // intentionally non-cancellable inside PublishAsync). Use None for the file
                // delete so a panel-close mid-deploy still cleans up the local thumbnail.
                await screenshotService.DeleteScreenshotAsync(slotIndex, CancellationToken.None);

                return DeleteOutfitOutcome.Success;
            }
            catch (OperationCanceledException)
            {
                // Cancellation here means the deploy-window wait was aborted before any
                // network call — the server still has the slot, so the slot is correctly
                // reported as Canceled and the presenter restores the prior UI.
                return DeleteOutfitOutcome.Cancelled;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                return DeleteOutfitOutcome.Failed;
            }
        }

        private ConfirmationDialogParameter BuildDialogParams() =>
            new (OUTFIT_POPUP_DELETE_TEXT,
                OUTFIT_POPUP_DELETE_CANCEL_TEXT,
                OUTFIT_POPUP_DELETE_CONFIRM_TEXT, deleteIcon,
                false,
                false);
    }
}
