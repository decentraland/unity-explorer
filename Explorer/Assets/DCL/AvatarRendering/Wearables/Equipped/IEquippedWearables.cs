using System.Collections.Generic;
using DCL.AvatarRendering.Wearables.Components;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Equipped
{
    public interface IEquippedWearables : IReadOnlyEquippedWearables
    {
        void Equip(IWearable wearable);

        void UnEquip(IWearable wearable);

        void UnEquipAll();

        void SetHairColor(Color newColor);

        void SetEyesColor(Color newColor);

        void SetBodyshapeColor(Color newColor);

        IReadOnlyCollection<string> ForceRenderCategories { get; }
        void SetForceRender(IReadOnlyCollection<string> categories);

        void Clear();
    }
}
