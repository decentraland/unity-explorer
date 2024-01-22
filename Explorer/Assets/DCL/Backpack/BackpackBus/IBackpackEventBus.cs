using DCL.AvatarRendering.Wearables.Components;
using System;
using System.Collections.Generic;

namespace DCL.Backpack.BackpackBus
{
    public interface IBackpackEventBus
    {
        public event Action<IWearable> SelectEvent;
        public event Action<IWearable, IReadOnlyCollection<string>> EquipEvent;
        public event Action<IWearable, IReadOnlyCollection<string>> UnEquipEvent;
        public event Action<string[]> HideEvent;
        public event Action<string> FilterCategoryEvent;
        public event Action<string> SearchEvent;

        public void SendSelect(IWearable equipWearable);
        public void SendEquip(IWearable equipWearable, IReadOnlyCollection<string> forceRender);
        public void SendUnEquip(IWearable unEquipWearable, IReadOnlyCollection<string> forceRender);
        public void SendHide(string[] hideWearableCategories);
        public void SendFilterCategory(string category);
        public void SendSearch(string searchText);
    }
}
