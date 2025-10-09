using System;
using System.Collections.Generic;

namespace DCL.Backpack.AvatarSection.Outfits.Models
{
    [Serializable]
    public class OutfitsMetadata
    {
        public List<OutfitItem> outfits;
        public List<string> namesForExtraSlots;
    }
}