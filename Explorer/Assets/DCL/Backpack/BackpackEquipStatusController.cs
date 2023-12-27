using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using System.Collections.Generic;

namespace DCL.Backpack
{
    public class BackpackEquipStatusController : IBackpackEquipStatusController
    {
        private readonly IBackpackEventBus backpackEventBus;
        private readonly Dictionary<string, IWearable> equippedWearables = new ();

        public BackpackEquipStatusController(IBackpackEventBus backpackEventBus)
        {
            this.backpackEventBus = backpackEventBus;

            this.backpackEventBus.EquipEvent += SetWearableForCategory;
            this.backpackEventBus.UnEquipEvent += RemoveWearableForCategory;

            foreach (string category in WearablesConstants.CATEGORIES_PRIORITY)
                equippedWearables.Add(category, null);
        }

        public IWearable GetEquippedWearableForCategory(string category) =>
            equippedWearables[category];

        private void RemoveWearableForCategory(IWearable wearable) =>
            equippedWearables[wearable.GetCategory()] = null;

        private void SetWearableForCategory(IWearable wearable) =>
            equippedWearables[wearable.GetCategory()] = wearable;
    }

    public interface IBackpackEquipStatusController
    {
        IWearable GetEquippedWearableForCategory(string category);
    }
}
