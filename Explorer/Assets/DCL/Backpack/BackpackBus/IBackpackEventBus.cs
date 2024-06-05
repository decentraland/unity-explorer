using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.CharacterPreview;
using DCL.UI;
using System;
using System.Collections.Generic;

namespace DCL.Backpack.BackpackBus
{
    public interface IBackpackEventBus
    {
        public event Action<IWearable> SelectWearableEvent;
        public event Action<IWearable, bool> EquipWearableEvent;
        public event Action<IWearable> UnEquipWearableEvent;
        public event Action<int, IEmote, bool> EquipEmoteEvent;
        public event Action<int, IEmote?> UnEquipEmoteEvent;
        public event Action<int> EmoteSlotSelectEvent;
        public event Action<IEmote> SelectEmoteEvent;
        public event Action<IReadOnlyCollection<string>, bool> ForceRenderEvent;
        public event Action<string> FilterCategoryEvent;
        public event Action<string> SearchEvent;
        public event Action<BackpackSections> ChangedBackpackSectionEvent;
        public event Action UnEquipAllEvent;
        event Action PublishProfileEvent;

        public void SendWearableSelect(IWearable equipWearable);

        public void SendEquipWearable(IWearable equipWearable, bool isInitialEquip = false);

        public void SendUnEquipWearable(IWearable unEquipWearable);

        public void SendUnEquipAll();

        public void SendForceRender(IReadOnlyCollection<string> forceRender, bool isInitialHide = false);

        public void SendFilterCategory(string category, AvatarWearableCategoryEnum categoryEnum);

        public void SendSearch(string searchText);

        void SendPublishProfile();

        void SendUnEquipEmote(int slot, IEmote? emote);

        void SendEquipEmote(int slot, IEmote emote, bool isInitialHide = false);

        public void SendEmoteSelect(IEmote emote);

        void SendEmoteSlotSelect(int slot);

        public void SendChangedBackpackSectionEvent(BackpackSections backpackSections);
    }
}
