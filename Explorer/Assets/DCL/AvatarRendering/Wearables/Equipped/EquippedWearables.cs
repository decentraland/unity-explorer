using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Equipped
{
    public class EquippedWearables : IEquippedWearables
    {
        private readonly Dictionary<string, IWearable?> wearables = new ();

        public EquippedWearables()
        {
            foreach (string category in WearablesConstants.CATEGORIES_PRIORITY)
                wearables.Add(category, null);
        }

        public IWearable? Wearable(string category) =>
            wearables[category];

        public bool IsEquipped(IWearable wearable) =>
            wearables[wearable.GetCategory()] == wearable;

        public void Equip(IWearable wearable) =>
            wearables[wearable.GetCategory()] = wearable;

        public void UnEquip(IWearable wearable)
        {
            if (IsEquipped(wearable) == false)
                throw new InvalidOperationException($"Trying to unequip a wearable that is not equipped. {wearable.GetCategory()}");

            wearables[wearable.GetCategory()] = null;
        }

        public IReadOnlyDictionary<string, IWearable?> Items() =>
            wearables;
    }
}
