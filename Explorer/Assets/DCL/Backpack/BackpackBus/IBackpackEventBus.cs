using DCL.AvatarRendering.Wearables.Components;
using System;

namespace DCL.Backpack.BackpackBus
{
    public interface IBackpackEventBus
    {
        public event Action<IWearable> SelectEvent;
        public event Action<IWearable> EquipEvent;
        public event Action<IWearable> UnEquipEvent;
        public event Action<string[]> HideEvent;
        public event Action<string> FilterCategoryEvent;
        public event Action<string> SearchEvent;

        public void SendSelect(IWearable equipWearable);
        public void SendEquip(IWearable equipWearable);
        public void SendUnEquip(IWearable unEquipWearable);
        public void SendHide(string[] hideWearableCategories);
        public void SendFilterCategory(string category);
        public void SendSearch(string searchText);
    }
}
