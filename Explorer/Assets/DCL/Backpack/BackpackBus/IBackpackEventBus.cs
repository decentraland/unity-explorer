using DCL.AvatarRendering.Wearables.Components;
using DCL.CharacterPreview;
using System;
using System.Collections.Generic;

namespace DCL.Backpack.BackpackBus
{
    public interface IBackpackEventBus
    {
        public event Action<IWearable> SelectEvent;
        public event Action<IWearable> EquipEvent;
        public event Action<IWearable> UnEquipEvent;
        public event Action<IReadOnlyCollection<string>> ForceRenderEvent;
        public event Action<string> FilterCategoryEvent;
        public event Action<string> SearchEvent;
        event Action PublishProfileEvent;

        public void SendSelect(IWearable equipWearable);

        public void SendEquip(IWearable equipWearable);

        public void SendUnEquip(IWearable unEquipWearable);

        public void SendForceRender(IReadOnlyCollection<string> forceRender);

        public void SendFilterCategory(string category, AvatarWearableCategoryEnum categoryEnum);

        public void SendSearch(string searchText);

        void SendPublishProfile();
    }
}
