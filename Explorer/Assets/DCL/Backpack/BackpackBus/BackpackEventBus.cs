using DCL.AvatarRendering.Wearables.Components;
using System;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackEventBus : IBackpackEventBus
    {
        public event Action<IWearable> SelectEvent;
        public event Action<IWearable> EquipEvent;
        public event Action<IWearable> UnEquipEvent;
        public event Action<string[]> HideEvent;
        public event Action<string> FilterCategoryEvent;
        public event Action<string> SearchEvent;

        public void SendSelect(IWearable equipWearable) =>
            SelectEvent?.Invoke(equipWearable);

        public void SendEquip(IWearable equipWearable) =>
            EquipEvent?.Invoke(equipWearable);

        public void SendUnEquip(IWearable unEquipWearable) =>
            UnEquipEvent?.Invoke(unEquipWearable);

        public void SendHide(string[] hideWearableCategories) =>
            HideEvent?.Invoke(hideWearableCategories);

        public void SendFilterCategory(string category) =>
            FilterCategoryEvent?.Invoke(category);

        public void SendSearch(string searchText) =>
            SearchEvent?.Invoke(searchText);
    }
}
