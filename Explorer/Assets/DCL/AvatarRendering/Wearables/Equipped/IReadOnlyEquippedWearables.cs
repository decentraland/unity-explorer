using DCL.AvatarRendering.Wearables.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Equipped
{
    public interface IReadOnlyEquippedWearables
    {
        IWearable? Wearable(string category);

        bool IsEquipped(IWearable wearable);

        IReadOnlyDictionary<string, IWearable?> Items();
    }
}
