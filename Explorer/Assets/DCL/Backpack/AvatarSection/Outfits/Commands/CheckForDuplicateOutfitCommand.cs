using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Diagnostics;
using DCL.UI.ConfirmationDialog.Opener;
using MVC;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public enum DuplicateCheckOutcome { NotDuplicate, DuplicateFoundAndAborted, Error }

    public class CheckForDuplicateOutfitCommand
    {
        private const string OUTFIT_POPUP_DELETE_CANCEL_TEXT = "CANCEL";
        private const string OUTFIT_POPUP_DELETE_CONFIRM_TEXT = "OK";

        private readonly CheckOutfitEquippedStateCommand checkEquippedStateCommand;

        public CheckForDuplicateOutfitCommand(CheckOutfitEquippedStateCommand checkEquippedStateCommand)
        {
            this.checkEquippedStateCommand = checkEquippedStateCommand;
        }

        public async UniTask<DuplicateCheckOutcome> ExecuteAsync(IEquippedWearables equippedWearables,
            IReadOnlyCollection<OutfitItem> currentOutfits,
            CancellationToken ct)
        {
            try
            {
                // First, find if a duplicate exists.
                int duplicateSlotIndex = -1;
                foreach (var outfitItem in currentOutfits)
                {
                    if (await checkEquippedStateCommand.ExecuteAsync(outfitItem, equippedWearables, ct))
                    {
                        duplicateSlotIndex = outfitItem.slot;
                        break;
                    }
                }

                // If no duplicate was found, we can proceed.
                if (duplicateSlotIndex == -1)
                    return DuplicateCheckOutcome.NotDuplicate;

                // If a duplicate is found, show an informational popup with only an "OK" button.
                var dialogParams = new ConfirmationDialogParameter($"Outfit already saved in Slot {duplicateSlotIndex + 1}",
                    OUTFIT_POPUP_DELETE_CANCEL_TEXT,
                    OUTFIT_POPUP_DELETE_CONFIRM_TEXT,
                    null, false, false);

                // We show the dialog and wait for the user to click "OK".
                await ViewDependencies
                    .ConfirmationDialogOpener
                    .OpenConfirmationDialogAsync(dialogParams, ct);

                return DuplicateCheckOutcome.DuplicateFoundAndAborted;
            }
            catch (OperationCanceledException)
            {
                return DuplicateCheckOutcome.DuplicateFoundAndAborted;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                return DuplicateCheckOutcome.Error;
            }
        }
    }
}