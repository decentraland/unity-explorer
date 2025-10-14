using System.Collections.Generic;
using DCL.Backpack.AvatarSection.Outfits.Models;

namespace DCL.Backpack.AvatarSection.Outfits
{
    public class OutfitsCollection
    {
        private readonly List<OutfitItem> outfits = new();

        public IReadOnlyList<OutfitItem> Get()
        {
            return outfits;
        }

        public void Update(IEnumerable<OutfitItem> newOutfits)
        {
            outfits.Clear();
            outfits.AddRange(newOutfits);
        }

        public void AddOrReplace(OutfitItem outfitItem)
        {
            int existingIndex = outfits.FindIndex(o => o.slot == outfitItem.slot);
            if (existingIndex != -1)
                outfits[existingIndex] = outfitItem;
            else
                outfits.Add(outfitItem);
        }

        public void Remove(int slotIndex)
        {
            outfits.RemoveAll(o => o.slot == slotIndex);
        }
    }
}