using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.CharacterPreview;
using System;
using System.Collections.Generic;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackEventBus : IBackpackEventBus
    {
        public event Action<IWearable> SelectWearableEvent;
        public event Action<IWearable> EquipWearableEvent;
        public event Action<IWearable> UnEquipWearableEvent;
        public event Action<int, IEmote>? EquipEmoteEvent;
        public event Action<int, IEmote?>? UnEquipEmoteEvent;
        public event Action<int>? EmoteSlotSelectEvent;
        public event Action<IEmote>? SelectEmoteEvent;
        public event Action<IReadOnlyCollection<string>> ForceRenderEvent;
        public event Action<string> FilterCategoryEvent;
        public event Action<AvatarWearableCategoryEnum> FilterCategoryByEnumEvent;
        public event Action PublishProfileEvent;

        public event Action<string> SearchEvent;

        public void SendWearableSelect(IWearable equipWearable) =>
            SelectWearableEvent?.Invoke(equipWearable);

        public void SendEquipWearable(IWearable equipWearable) =>
            EquipWearableEvent?.Invoke(equipWearable);

        public void SendUnEquipWearable(IWearable unEquipWearable) =>
            UnEquipWearableEvent?.Invoke(unEquipWearable);

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

        public void SendUnEquipEmote(int slot, IEmote? emote) =>
            UnEquipEmoteEvent?.Invoke(slot, emote);

        public void SendEquipEmote(int slot, IEmote emote) =>
            EquipEmoteEvent?.Invoke(slot, emote);

        public void SendEmoteSelect(IEmote emote) =>
            SelectEmoteEvent?.Invoke(emote);

        public void SendEmoteSlotSelect(int slot) =>
            EmoteSlotSelectEvent?.Invoke(slot);
    }
}
