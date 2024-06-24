using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.CharacterPreview;
using DCL.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Backpack.BackpackBus
{
    public interface IBackpackEventBus
    {
        public event Action<IWearable> SelectWearableEvent;
        public event Action<IWearable> EquipWearableEvent;
        public event Action<IWearable> UnEquipWearableEvent;
        public event Action<int, IEmote> EquipEmoteEvent;
        public event Action<int, IEmote?> UnEquipEmoteEvent;
        public event Action<int> EmoteSlotSelectEvent;
        public event Action<IEmote> SelectEmoteEvent;
        public event Action<IReadOnlyCollection<string>> ForceRenderEvent;
        public event Action<string> FilterCategoryEvent;
        public event Action<string> SearchEvent;
        public event Action<BackpackSections> ChangedBackpackSectionEvent;
        public event Action<Color,string>? ChangeColorEvent;
        public event Action UnEquipAllEvent;
        event Action PublishProfileEvent;

        public void SendWearableSelect(IWearable equipWearable);

        public void SendEquipWearable(IWearable equipWearable);

        public void SendUnEquipWearable(IWearable unEquipWearable);

        public void SendUnEquipAll();

        public void SendChangeColor(Color newColor, string category);

        public void SendForceRender(IReadOnlyCollection<string> forceRender);

        public void SendFilterCategory(string category, AvatarWearableCategoryEnum categoryEnum);

        public void SendSearch(string searchText);

        void SendPublishProfile();

        void SendUnEquipEmote(int slot, IEmote? emote);

        void SendEquipEmote(int slot, IEmote emote);

        public void SendEmoteSelect(IEmote emote);

        void SendEmoteSlotSelect(int slot);

        public void SendChangedBackpackSectionEvent(BackpackSections backpackSections);
    }
}
