using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using System;
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

        public bool IsWearableEquipped(IWearable wearable) =>
            equippedWearables[wearable.GetCategory()] == wearable;

        //This will retrieve the list of default hides for the current equipped wearables
        //Manual hide override will be a separate task
        //TODO retrieve logic from old renderer
        public List<string> GetCurrentWearableHides()
        {
            List<string> hides = new List<string>();

            foreach (string category in WearablesConstants.CATEGORIES_PRIORITY)
            {
                IWearable wearable = equippedWearables[category];

                if (wearable == null)
                    continue;
            }

            return hides;
        }

        private void RemoveWearableForCategory(IWearable wearable, IReadOnlyCollection<string> readOnlyCollection) =>
            equippedWearables[wearable.GetCategory()] = null;

        private void SetWearableForCategory(IWearable wearable, IReadOnlyCollection<string> readOnlyCollection) =>
            equippedWearables[wearable.GetCategory()] = wearable;
    }

    public interface IBackpackEquipStatusController
    {
        IWearable GetEquippedWearableForCategory(string category);
        bool IsWearableEquipped(IWearable wearable);
    }
}
