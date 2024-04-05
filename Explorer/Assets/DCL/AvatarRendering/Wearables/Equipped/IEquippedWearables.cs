using DCL.AvatarRendering.Wearables.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Equipped
{
    public interface IEquippedWearables
    {
        IWearable? Wearable(string category);

        bool IsEquipped(IWearable wearable);

        void Equip(IWearable wearable);

        void UnEquip(IWearable wearable);

        IReadOnlyDictionary<string, IWearable?> Items();
    }
}
