using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.CharacterPreview;
using System;
using System.Collections.Generic;

namespace DCL.Backpack.BackpackBus
{
    public interface IBackpackEventBus
    {
        public event Action<IWearable> SelectEvent;
        public event Action<IWearable> EquipWearableEvent;
        public event Action<IWearable> UnEquipWearableEvent;
        public event Action<int, IEmote> EquipEmoteEvent;
        public event Action<int> UnEquipEmoteEvent;
        public event Action<IReadOnlyCollection<string>> ForceRenderEvent;
        public event Action<string> FilterCategoryEvent;
        public event Action<string> SearchEvent;
        event Action PublishProfileEvent;

        public void SendSelect(IWearable equipWearable);

        public void SendEquipWearable(IWearable equipWearable);

        public void SendUnEquipWearable(IWearable unEquipWearable);

        public void SendForceRender(IReadOnlyCollection<string> forceRender);

        public void SendFilterCategory(string category, AvatarWearableCategoryEnum categoryEnum);

        public void SendSearch(string searchText);

        void SendPublishProfile();

        void SendUnEquipEmote(int slot);

        void SendEquipEmote(int slot, IEmote emote);
    }
}
