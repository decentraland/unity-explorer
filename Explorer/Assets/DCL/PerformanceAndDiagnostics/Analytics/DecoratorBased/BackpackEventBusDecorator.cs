using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
using DCL.UI;
using Segment.Serialization;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class BackpackEventBusAnalyticsDecorator : IBackpackEventBus
    {
        private readonly IBackpackEventBus core;
        private readonly IAnalyticsController analytics;

        public event Action<IWearable> SelectWearableEvent;
        public event Action<IWearable> EquipWearableEvent;
        public event Action<IWearable> UnEquipWearableEvent;
        public event Action<int, IEmote, bool> EquipEmoteEvent;
        public event Action<int, IEmote> UnEquipEmoteEvent;
        public event Action<int> EmoteSlotSelectEvent;
        public event Action<IEmote> SelectEmoteEvent;
        public event Action<IReadOnlyCollection<string>> ForceRenderEvent;
        public event Action<string> FilterCategoryEvent;
        public event Action<string> SearchEvent;
        public event Action<BackpackSections> ChangedBackpackSectionEvent;
        public event Action<Color, string> ChangeColorEvent;
        public event Action UnEquipAllEvent;
        public event Action PublishProfileEvent;
        public event Action<AvatarWearableCategoryEnum> FilterCategoryByEnumEvent;
        public event Action DeactivateEvent;

        public BackpackEventBusAnalyticsDecorator(IBackpackEventBus core, IAnalyticsController analytics)
        {
            this.core = core;
            this.analytics = analytics;

            core.EquipEmoteEvent += (slot, emote, manuallyEquipped) =>
            {
                EquipEmoteEvent?.Invoke(slot, emote, manuallyEquipped);

                if (manuallyEquipped)
                {
                    var emoteUrn = emote.GetUrn().ToString();

                    analytics.Track(AnalyticsEvents.Wearables.USED_EMOTE, new JsonObject
                    {
                        { "item_id", emoteUrn }, // Id of the item <contract-address>-<item_id>
                        { "is_base", !emoteUrn.StartsWith("urn:") },
                        { "name", emote.GetName() },
                        { "emote_index", slot },
                    });
                }
            };

            // Re-emit core events
            core.SelectWearableEvent += wearable => SelectWearableEvent?.Invoke(wearable);
            core.EquipWearableEvent += wearable => EquipWearableEvent?.Invoke(wearable);
            core.UnEquipWearableEvent += wearable => UnEquipWearableEvent?.Invoke(wearable);
            core.UnEquipEmoteEvent += (slot, emote) => UnEquipEmoteEvent?.Invoke(slot, emote);
            core.EmoteSlotSelectEvent += slot => EmoteSlotSelectEvent?.Invoke(slot);
            core.SelectEmoteEvent += emote => SelectEmoteEvent?.Invoke(emote);
            core.ForceRenderEvent += forceRender => ForceRenderEvent?.Invoke(forceRender);
            core.FilterCategoryEvent += category => FilterCategoryEvent?.Invoke(category);
            core.SearchEvent += searchText => SearchEvent?.Invoke(searchText);
            core.ChangedBackpackSectionEvent += section => ChangedBackpackSectionEvent?.Invoke(section);
            core.ChangeColorEvent += (color, category) => ChangeColorEvent?.Invoke(color, category);
            core.UnEquipAllEvent += () => UnEquipAllEvent?.Invoke();
            core.PublishProfileEvent += () => PublishProfileEvent?.Invoke();
            core.FilterCategoryByEnumEvent += categoryEnum => FilterCategoryByEnumEvent?.Invoke(categoryEnum);
            core.DeactivateEvent += () => DeactivateEvent?.Invoke();
        }

        public void SendUnEquipAll() =>
            core.SendUnEquipAll();

        public void SendChangeColor(Color newColor, string category) =>
            core.SendChangeColor(newColor, category);

        public void SendForceRender(IReadOnlyCollection<string> forceRender) =>
            core.SendForceRender(forceRender);

        public void SendSearch(string searchText) =>
            core.SendSearch(searchText);

        public void SendPublishProfile() =>
            core.SendPublishProfile();

        public void SendEmoteSlotSelect(int slot) =>
            core.SendEmoteSlotSelect(slot);

        public void SendBackpackDeactivateEvent() =>
            core.SendBackpackDeactivateEvent();

        public void SendChangedBackpackSectionEvent(BackpackSections backpackSections) =>
            core.SendChangedBackpackSectionEvent(backpackSections);

        public void SendEmoteSelect(IEmote emote) =>
            core.SendEmoteSelect(emote);

        public void SendEquipEmote(int slot, IEmote emote, bool isManuallyEquipped) =>
            core.SendEquipEmote(slot, emote, isManuallyEquipped);

        public void SendUnEquipEmote(int slot, IEmote? emote) =>
            core.SendUnEquipEmote(slot, emote);

        public void SendFilterCategory(string category, AvatarWearableCategoryEnum categoryEnum) =>
            core.SendFilterCategory(category, categoryEnum);

        public void SendUnEquipWearable(IWearable unEquipWearable) =>
            core.SendUnEquipWearable(unEquipWearable);

        public void SendEquipWearable(IWearable equipWearable) =>
            core.SendEquipWearable(equipWearable);

        public void SendWearableSelect(IWearable equipWearable) =>
            core.SendWearableSelect(equipWearable);
    }
}
