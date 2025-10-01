using CodeLess.Interfaces;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.CharacterPreview;
using DCL.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Backpack.BackpackBus
{
    [AutoInterface]
    public class BackpackEventBus : IBackpackEventBus
    {
        public event Action<IWearable>? SelectWearableEvent;
        public event Action<IWearable>? EquipWearableEvent;
        public event Action<IWearable>? UnEquipWearableEvent;
        public event Action<int, IEmote, bool>? EquipEmoteEvent;
        public event Action<int, IEmote?>? UnEquipEmoteEvent;
        public event Action<int>? EmoteSlotSelectEvent;
        public event Action<IEmote>? SelectEmoteEvent;
        public event Action<IReadOnlyCollection<string>>? ForceRenderEvent;
        public event Action<BackpackSections>? ChangedBackpackSectionEvent;
        public event Action? DeactivateEvent;
        public event Action? UnEquipAllEvent;
        public event Action<Color, string>? ChangeColorEvent;
        public event Action? PublishProfileEvent;
        public event Action<string?, AvatarWearableCategoryEnum?, string?>? FilterEvent;

        public void SendWearableSelect(IWearable equipWearable) =>
            SelectWearableEvent?.Invoke(equipWearable);

        public void SendEquipWearable(IWearable equipWearable) =>
            EquipWearableEvent?.Invoke(equipWearable);

        public void SendUnEquipWearable(IWearable unEquipWearable) =>
            UnEquipWearableEvent?.Invoke(unEquipWearable);

        public void SendUnEquipAll() =>
            UnEquipAllEvent?.Invoke();

        public void SendChangeColor(Color newColor, string category) =>
            ChangeColorEvent?.Invoke(newColor, category);

        public void SendForceRender(IReadOnlyCollection<string> forceRender) =>
            ForceRenderEvent?.Invoke(forceRender);

        public void SendFilter(string? category, AvatarWearableCategoryEnum? categoryEnum, string? searchText) =>
            FilterEvent?.Invoke(category, categoryEnum, searchText);

        public void SendPublishProfile() =>
            PublishProfileEvent?.Invoke();

        public void SendUnEquipEmote(int slot, IEmote? emote) =>
            UnEquipEmoteEvent?.Invoke(slot, emote);

        public void SendEquipEmote(int slot, IEmote emote, bool isManuallyEquipped) =>
            EquipEmoteEvent?.Invoke(slot, emote, isManuallyEquipped);

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
