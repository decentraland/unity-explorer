using DCL.AvatarRendering.Wearables.Components;
using System;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackEventBus : IBackpackEventBus
    {
        public event Action<IWearable> EquipEvent;
        public event Action<IWearable> UnEquipEvent;
        public event Action<string[]> HideEvent;

        public void SendEquip(IWearable equipWearable)
        {
            EquipEvent?.Invoke(equipWearable);
        }

        public void SendUnEquip(IWearable unEquipWearable)
        {
            UnEquipEvent?.Invoke(unEquipWearable);
        }

        public void SendHide(string[] hideWearableCategories)
        {
            HideEvent?.Invoke(hideWearableCategories);
        }
    }
}
