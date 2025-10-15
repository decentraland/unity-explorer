using System.Collections.Generic;
using DCL.Backpack.AvatarSection.Outfits.Models;

namespace DCL.Backpack.AvatarSection.Outfits
{
    public class OutfitsCollection
    {
        private readonly Dictionary<int, OutfitItem> outfitsBySlot = new ();

        public IReadOnlyCollection<OutfitItem> GetAll()
        {
            return outfitsBySlot.Values;
        }

        public bool TryGet(int slotIndex, out OutfitItem outfitItem)
        {
            return outfitsBySlot.TryGetValue(slotIndex, out outfitItem);
        }

        public void Update(IEnumerable<OutfitItem> newOutfits)
        {
            outfitsBySlot.Clear();
            foreach (var outfit in newOutfits)
                outfitsBySlot[outfit.slot] = outfit;
        }

        public void AddOrReplace(OutfitItem outfitItem)
        {
            outfitsBySlot[outfitItem.slot] = outfitItem;
        }

        public void Remove(int slotIndex)
        {
            outfitsBySlot.Remove(slotIndex);
        }
    }
}