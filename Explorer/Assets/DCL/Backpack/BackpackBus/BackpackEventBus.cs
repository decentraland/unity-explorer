using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.CharacterPreview;
using DCL.UI;
using System;
using System.Collections.Generic;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackEventBus : IBackpackEventBus
    {
        public event Action<IWearable> SelectWearableEvent;
        public event Action<IWearable, bool> EquipWearableEvent;
        public event Action<IWearable> UnEquipWearableEvent;
        public event Action<int, IEmote, bool>? EquipEmoteEvent;
        public event Action<int, IEmote?>? UnEquipEmoteEvent;
        public event Action<int>? EmoteSlotSelectEvent;
        public event Action<IEmote>? SelectEmoteEvent;
        public event Action<IReadOnlyCollection<string>, bool> ForceRenderEvent;
        public event Action<string> FilterCategoryEvent;
        public event Action<AvatarWearableCategoryEnum> FilterCategoryByEnumEvent;
        public event Action<BackpackSections>? ChangedBackpackSectionEvent;
        public event Action? DeactivateEvent;
        public event Action UnEquipAllEvent;
        public event Action PublishProfileEvent;

        public event Action<string> SearchEvent;

        public void SendWearableSelect(IWearable equipWearable) =>
            SelectWearableEvent?.Invoke(equipWearable);

        public void SendEquipWearable(IWearable equipWearable, bool isInitialEquip = false) =>
            EquipWearableEvent?.Invoke(equipWearable, isInitialEquip);

        public void SendUnEquipWearable(IWearable unEquipWearable) =>
            UnEquipWearableEvent?.Invoke(unEquipWearable);

        public void SendUnEquipAll() =>
            UnEquipAllEvent?.Invoke();

        public void SendForceRender(IReadOnlyCollection<string> forceRender, bool isInitialHide = false) =>
            ForceRenderEvent?.Invoke(forceRender, isInitialHide);

        public void SendFilterCategory(string category, AvatarWearableCategoryEnum categoryEnum)
        {
            FilterCategoryEvent?.Invoke(category);
            FilterCategoryByEnumEvent?.Invoke(categoryEnum);
        }

        public void SendSearch(string searchText) =>
            SearchEvent?.Invoke(searchText);

        public void SendPublishProfile() =>
            PublishProfileEvent?.Invoke();

        public void SendUnEquipEmote(int slot, IEmote? emote) =>
            UnEquipEmoteEvent?.Invoke(slot, emote);

        public void SendEquipEmote(int slot, IEmote emote, bool isInitialEquip = false) =>
            EquipEmoteEvent?.Invoke(slot, emote, isInitialEquip);

        public void SendEmoteSelect(IEmote emote) =>
            SelectEmoteEvent?.Invoke(emote);

        public void SendEmoteSlotSelect(int slot) =>
            EmoteSlotSelectEvent?.Invoke(slot);

        public void SendChangedBackpackSectionEvent(BackpackSections backpackSections) =>
            ChangedBackpackSectionEvent?.Invoke(backpackSections);

        public void SendBackpackDeactivateEvent() =>
            DeactivateEvent?.Invoke();

    }
}
