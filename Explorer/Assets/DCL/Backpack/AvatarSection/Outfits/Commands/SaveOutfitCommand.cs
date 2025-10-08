using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Backpack.Slots;
using DCL.Diagnostics;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public enum SaveOutfitOutcome
    {
        Success,
        Cancelled,
        Failed
    }

    public sealed class SaveOutfitCommand
    {
        private readonly IOutfitsService outfitService;

        public SaveOutfitCommand(IOutfitsService outfitService)
        {
            this.outfitService = outfitService;
        }

        public async UniTask<SaveOutfitOutcome> ExecuteAsync(
            int slotIndex,
            OutfitData outfit,
            CancellationToken ct,
            Action? onConfirmed = null)
        {
            onConfirmed?.Invoke();

            try
            {
                var saved = await outfitService.SaveOutfitAsync(slotIndex, outfit, ct);
                return SaveOutfitOutcome.Success;
            }
            catch (OperationCanceledException)
            {
                return SaveOutfitOutcome.Cancelled;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                return SaveOutfitOutcome.Failed;
            }
        }
    }
}