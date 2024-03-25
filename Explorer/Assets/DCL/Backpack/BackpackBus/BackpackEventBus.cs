using DCL.AvatarRendering.Wearables.Components;
using DCL.CharacterPreview;
using System;
using System.Collections.Generic;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackEventBus : IBackpackEventBus
    {
        public event Action<IWearable> SelectEvent;
        public event Action<IWearable> EquipEvent;
        public event Action<IWearable> UnEquipEvent;
        public event Action<IReadOnlyCollection<string>> ForceRenderEvent;
        public event Action<string> FilterCategoryEvent;
        public event Action<AvatarWearableCategoryEnum> FilterCategoryByEnumEvent;
        public event Action PublishProfileEvent;

        public event Action<string> SearchEvent;

        public void SendSelect(IWearable equipWearable) =>
            SelectEvent?.Invoke(equipWearable);

        public void SendEquip(IWearable equipWearable) =>
            EquipEvent?.Invoke(equipWearable);

        public void SendUnEquip(IWearable unEquipWearable) =>
            UnEquipEvent?.Invoke(unEquipWearable);

        public void SendForceRender(IReadOnlyCollection<string> forceRender) =>
            ForceRenderEvent?.Invoke(forceRender);

        public void SendFilterCategory(string category, AvatarWearableCategoryEnum categoryEnum)
        {
            FilterCategoryEvent?.Invoke(category);
            FilterCategoryByEnumEvent?.Invoke(categoryEnum);
        }

        public void SendSearch(string searchText) =>
            SearchEvent?.Invoke(searchText);

        public void SendPublishProfile() =>
            PublishProfileEvent?.Invoke();
    }
}
