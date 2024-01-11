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

        public void SendSelect(IWearable equipWearable);
        public void SendEquip(IWearable equipWearable);
        public void SendUnEquip(IWearable unEquipWearable);
        public void SendHide(string[] hideWearableCategories);
    }
}
