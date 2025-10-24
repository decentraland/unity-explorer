using System.Collections.Generic;

namespace DCL.Backpack.AvatarSection.Outfits.Events
{
    public class OutfitsEvents
    {
        public struct SaveOutfitEvent
        {
            public IReadOnlyList<string> WearablesUrns { get; }

            public SaveOutfitEvent(IReadOnlyList<string> wearablesUrns)
            {
                WearablesUrns = wearablesUrns;
            }
        }

        public struct EquipOutfitEvent
        {
        }

        public struct ClaimExtraOutfitsEvent
        {
        }
    }
}