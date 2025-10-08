using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Slots;

namespace DCL.Backpack.AvatarSection.Outfits.Services
{
    public interface IOutfitsService
    {
        UniTask<Dictionary<int, OutfitData>> GetOutfitsAsync(CancellationToken ct);
        UniTask<OutfitData> SaveOutfitAsync(int slotIndex, OutfitData outfit, CancellationToken ct);
        UniTask DeleteOutfitAsync(int slotIndex, CancellationToken ct);
        UniTask<bool> ShouldShowExtraOutfitSlotsAsync(CancellationToken ct);
    }
}