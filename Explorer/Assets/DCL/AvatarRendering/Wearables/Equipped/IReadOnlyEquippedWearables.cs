using DCL.AvatarRendering.Wearables.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Equipped
{
    public interface IReadOnlyEquippedWearables
    {
        IWearable? Wearable(string category);

        (Color, Color, Color) GetColors();

        bool IsEquipped(IWearable wearable);

        IReadOnlyDictionary<string, IWearable?> Items();
    }
}
