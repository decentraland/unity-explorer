using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
using DCL.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using DCL.Backpack.AvatarSection.Outfits.Commands;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class BackpackEventBusAnalyticsDecorator : IBackpackEventBus
    {
        // The same values the places, events and friends events carry to tell the two surfaces apart
        private const string EXPLORE_SOURCE = "explore";
        private const string LOBBY_SOURCE = "lobby";

        private readonly IBackpackEventBus core;
        private readonly IAnalyticsController analytics;

        // Kept from the last time the panel was shown, since the profile it publishes on hiding lands after it moved back home
        private string source = EXPLORE_SOURCE;

        public event Action<IWearable> SelectWearableEvent;
        public event Action<IWearable, bool> EquipWearableEvent;
        public event Action<IWearable> UnEquipWearableEvent;
        public event Action<int, IEmote, bool> EquipEmoteEvent;
        public event Action<int, IEmote> UnEquipEmoteEvent;
        public event Action<int> EmoteSlotSelectEvent;
        public event Action<IEmote> SelectEmoteEvent;
        public event Action<IReadOnlyCollection<string>> ForceRenderEvent;
        public event Action<string?, AvatarWearableCategoryEnum?, string?> FilterEvent;
        public event Action<BackpackEquipOutfitCommand, IReadOnlyCollection<IWearable>>? EquipOutfitEvent;
        public event Action? EquipOutfitCompletedEvent;
        public event Action<BackpackSections> ChangedBackpackSectionEvent;
        public event Action? UnEquipAllWearablesEvent;
        public event Action<Color, string> ChangeColorEvent;
        public event Action UnEquipAllEvent;
        public event Action PublishProfileEvent;
        public event Action? AvatarChangedEvent;
        public event Action<bool>? ActivateEvent;
        public event Action DeactivateEvent;

        public BackpackEventBusAnalyticsDecorator(IBackpackEventBus core, IAnalyticsController analytics)
        {
            this.core = core;
            this.analytics = analytics;

            core.EquipEmoteEvent += ReEmitWithAnalytics;

            // Re-emit core events
            core.SelectWearableEvent += OnSelectWearable;
            core.EquipWearableEvent += OnEquipWearable;
            core.UnEquipWearableEvent += OnUnEquipWearable;
            core.UnEquipEmoteEvent += OnUnEquipEmote;
            core.EmoteSlotSelectEvent += OnEmoteSlotSelect;
            core.SelectEmoteEvent += OnSelectEmote;
            core.ForceRenderEvent += OnForceRender;
            core.FilterEvent += OnFilter;
            core.ChangedBackpackSectionEvent += OnChangedBackpackSection;
            core.ChangeColorEvent += OnChangeColor;
            core.UnEquipAllEvent += OnUnEquipAll;
            core.PublishProfileEvent += OnPublishProfile;
            core.AvatarChangedEvent += OnAvatarChanged;
            core.ActivateEvent += OnActivate;
            core.DeactivateEvent += OnDeactivate;
            core.UnEquipAllWearablesEvent += OnUnEquipAllWearables;
            core.EquipOutfitEvent += OnEquipOutfit;
            core.EquipOutfitCompletedEvent += OnEquipOutfitCompleted;
        }

        ~BackpackEventBusAnalyticsDecorator()
        {
            core.EquipEmoteEvent -= ReEmitWithAnalytics;

            // Unsubscribe from re-emited events
            core.SelectWearableEvent -= OnSelectWearable;
            core.EquipWearableEvent -= OnEquipWearable;
            core.UnEquipWearableEvent -= OnUnEquipWearable;
            core.UnEquipEmoteEvent -= OnUnEquipEmote;
            core.EmoteSlotSelectEvent -= OnEmoteSlotSelect;
            core.SelectEmoteEvent -= OnSelectEmote;
            core.ForceRenderEvent -= OnForceRender;
            core.FilterEvent -= OnFilter;
            core.ChangedBackpackSectionEvent -= OnChangedBackpackSection;
            core.ChangeColorEvent -= OnChangeColor;
            core.UnEquipAllEvent -= OnUnEquipAll;
            core.PublishProfileEvent -= OnPublishProfile;
            core.AvatarChangedEvent -= OnAvatarChanged;
            core.ActivateEvent -= OnActivate;
            core.DeactivateEvent -= OnDeactivate;
            core.UnEquipAllWearablesEvent -= OnUnEquipAllWearables;
            core.EquipOutfitEvent -= OnEquipOutfit;
            core.EquipOutfitCompletedEvent -= OnEquipOutfitCompleted;
        }

        private void OnEquipOutfit(BackpackEquipOutfitCommand command, IReadOnlyCollection<IWearable> wearables) =>
            EquipOutfitEvent?.Invoke(command, wearables);

        private void OnEquipOutfitCompleted() =>
            EquipOutfitCompletedEvent?.Invoke();

        private void ReEmitWithAnalytics(int slot, IEmote emote, bool manuallyEquipped)
        {
            EquipEmoteEvent?.Invoke(slot, emote, manuallyEquipped);

            if (!manuallyEquipped) return;

            var emoteUrn = emote.GetUrn().ToString();
            analytics.Track(AnalyticsEvents.Wearables.USED_EMOTE, new JObject
            {
                { "item_id", emoteUrn }, // Id of the item <contract-address>-<item_id>
                { "is_base", !Emote.IsOnChain(emoteUrn) },
                { "name", emote.GetName() },
                { "emote_index", slot },
                { "source", "backpack" },
            });
        }

        private void OnSelectWearable(IWearable wearable) => SelectWearableEvent?.Invoke(wearable);
        private void OnEquipWearable(IWearable wearable, bool isManuallyEquipped) => EquipWearableEvent?.Invoke(wearable, isManuallyEquipped);
        private void OnUnEquipWearable(IWearable wearable) => UnEquipWearableEvent?.Invoke(wearable);
        private void OnUnEquipEmote(int slot, IEmote emote) => UnEquipEmoteEvent?.Invoke(slot, emote);
        private void OnEmoteSlotSelect(int slot) => EmoteSlotSelectEvent?.Invoke(slot);
        private void OnSelectEmote(IEmote emote) => SelectEmoteEvent?.Invoke(emote);
        private void OnForceRender(IReadOnlyCollection<string> forceRender) => ForceRenderEvent?.Invoke(forceRender);
        private void OnFilter(string? category, AvatarWearableCategoryEnum? categoryEnum, string? searchText) => FilterEvent?.Invoke(category, categoryEnum, searchText);
        private void OnChangedBackpackSection(BackpackSections section) => ChangedBackpackSectionEvent?.Invoke(section);
        private void OnChangeColor(Color color, string category) => ChangeColorEvent?.Invoke(color, category);
        private void OnUnEquipAll() => UnEquipAllEvent?.Invoke();

        private void OnUnEquipAllWearables() =>
            UnEquipAllWearablesEvent?.Invoke();

        private void OnPublishProfile() =>
            PublishProfileEvent?.Invoke();

        private void OnAvatarChanged()
        {
            AvatarChangedEvent?.Invoke();

            analytics.Track(AnalyticsEvents.Backpack.AVATAR_CHANGED, new JObject { { "source", source } });
        }

        private void OnActivate(bool isInExplorePanel)
        {
            ActivateEvent?.Invoke(isInExplorePanel);

            // The lobby's modal is the only host that borrows the view out of the explore panel
            source = isInExplorePanel ? EXPLORE_SOURCE : LOBBY_SOURCE;
            analytics.Track(AnalyticsEvents.Backpack.BACKPACK_OPENED, new JObject { { "source", source } });
        }

        private void OnDeactivate() =>
            DeactivateEvent?.Invoke();

        public void SendUnEquipAll() =>
            core.SendUnEquipAll();

        public void SendUnEquipAllWearables() =>
            core.SendUnEquipAllWearables();

        public void SendChangeColor(Color newColor, string category) =>
            core.SendChangeColor(newColor, category);

        public void SendForceRender(IReadOnlyCollection<string> forceRender) =>
            core.SendForceRender(forceRender);

        public void SendFilter(string? category, AvatarWearableCategoryEnum? categoryEnum, string? searchText) =>
            core.SendFilter(category, categoryEnum, searchText);

        public void SendPublishProfile() =>
            core.SendPublishProfile();

        public void SendEmoteSlotSelect(int slot) =>
            core.SendEmoteSlotSelect(slot);

        public void SendAvatarChanged() =>
            core.SendAvatarChanged();

        public void SendBackpackActivateEvent(bool isInExplorePanel) =>
            core.SendBackpackActivateEvent(isInExplorePanel);

        public void SendBackpackDeactivateEvent() =>
            core.SendBackpackDeactivateEvent();

        public void SendEquipOutfit(BackpackEquipOutfitCommand command, IReadOnlyCollection<IWearable> wearables) =>
            core.SendEquipOutfit(command, wearables);

        public void SendEquipOutfitCompleted() =>
            core.SendEquipOutfitCompleted();

        public void SendChangedBackpackSectionEvent(BackpackSections backpackSections) =>
            core.SendChangedBackpackSectionEvent(backpackSections);

        public void SendEmoteSelect(IEmote emote) =>
            core.SendEmoteSelect(emote);

        public void SendEquipEmote(int slot, IEmote emote, bool isManuallyEquipped) =>
            core.SendEquipEmote(slot, emote, isManuallyEquipped);

        public void SendUnEquipEmote(int slot, IEmote? emote) =>
            core.SendUnEquipEmote(slot, emote);

        public void SendUnEquipWearable(IWearable unEquipWearable) =>
            core.SendUnEquipWearable(unEquipWearable);

        public void SendEquipWearable(IWearable equipWearable, bool isManuallyEquipped) =>
            core.SendEquipWearable(equipWearable, isManuallyEquipped);

        public void SendWearableSelect(IWearable equipWearable) =>
            core.SendWearableSelect(equipWearable);
    }
}
