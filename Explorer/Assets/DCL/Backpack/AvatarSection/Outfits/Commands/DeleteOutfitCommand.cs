using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.AvatarSection.Outfits.Repository;
using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Diagnostics;
using DCL.Profiles.Self;
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

        private readonly ISelfProfile selfProfile;
        private readonly OutfitsRepository outfitsRepository;
        private readonly IAvatarScreenshotService screenshotService;

        public DeleteOutfitCommand(ISelfProfile selfProfile,
            OutfitsRepository outfitsRepository,
            IAvatarScreenshotService screenshotService)
        {
            this.selfProfile = selfProfile;
            this.outfitsRepository = outfitsRepository;
            this.screenshotService = screenshotService;
        }

        public async UniTask<DeleteOutfitOutcome> ExecuteAsync(
            int slotIndex,
            IReadOnlyList<OutfitItem> currentOutfits,
            CancellationToken ct)
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
                var profile = await selfProfile.ProfileAsync(ct);
                if (profile == null)
                {
                    ReportHub.LogError(ReportCategory.OUTFITS, "Delete failed: user profile not found.");
                    return DeleteOutfitOutcome.Failed;
                }

                // Create the new state without the deleted outfit
                List<OutfitItem> updatedOutfits = currentOutfits.Where(o => o.slot != slotIndex).ToList();

                // First, deploy the metadata change. This is the most likely to fail.
                await outfitsRepository.SetAsync(profile, updatedOutfits, ct, noExtraSlots: true);

                // If server update succeeds, delete the local screenshot.
                await screenshotService.DeleteScreenshotAsync(slotIndex, ct);

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