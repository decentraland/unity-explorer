using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.Slots;

namespace DCL.Backpack.AvatarSection.Outfits.Services
{
    public interface IOutfitsService
    {
        // Fetches the outfits from the server. Returns a dictionary mapping slot index to OutfitData.
        UniTask<Dictionary<int, OutfitData>> GetOutfitsFromServerAsync(CancellationToken ct);

        // Initializes the service by loading data from the network. Controller calls this on Activate.
        UniTask LoadOutfitsAsync(CancellationToken ct);

        // Returns the current in-memory list of outfits for the UI to display.
        IReadOnlyList<OutfitItem> GetCurrentOutfits();

        // The controller calls this when the user saves or updates an outfit.
        void UpdateLocalOutfit(OutfitItem outfitToSave);

        // The controller calls this when the user deletes an outfit.
        void DeleteLocalOutfit(int slotIndex);

        // The controller calls this on Deactivate to push changes to the server.
        UniTask DeployOutfitsIfDirtyAsync(CancellationToken ct);

        // This can remain for checking banner visibility etc.
        UniTask<bool> ShouldShowExtraOutfitSlotsAsync(CancellationToken ct);
    }
}