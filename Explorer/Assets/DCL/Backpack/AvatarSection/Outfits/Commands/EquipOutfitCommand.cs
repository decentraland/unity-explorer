using DCL.Backpack.Slots;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public readonly struct EquipOutfitCommand
    {
        public readonly OutfitData OutfitData;
        public EquipOutfitCommand(OutfitData outfitData) { OutfitData = outfitData; }
    }
}