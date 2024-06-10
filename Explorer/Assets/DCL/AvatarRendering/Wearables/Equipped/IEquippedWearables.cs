using DCL.AvatarRendering.Wearables.Components;

namespace DCL.AvatarRendering.Wearables.Equipped
{
    public interface IEquippedWearables : IReadOnlyEquippedWearables
    {
        void Equip(IWearable wearable);

        void UnEquip(IWearable wearable);

        void UnEquipAll();
    }
}
