using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
using DCL.UI;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Backpack.CharacterPreview
{
    public class BackpackCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly BackpackEventBus backpackEventBus;
        private readonly IEquippedEmotes equippedEmotes;

        public BackpackCharacterPreviewController(CharacterPreviewView view,
            ICharacterPreviewFactory previewFactory,
            BackpackEventBus backpackEventBus,
            World world,
            IEquippedEmotes equippedEmotes)
            : base(view, previewFactory, world)
        {
            this.backpackEventBus = backpackEventBus;
            this.equippedEmotes = equippedEmotes;
            backpackEventBus.EquipWearableEvent += OnWearableEquipped;
            backpackEventBus.UnEquipWearableEvent += OnWearableUnequipped;
            backpackEventBus.EquipEmoteEvent += OnEmoteEquipped;
            backpackEventBus.SelectEmoteEvent += OnEmoteSelected;
            backpackEventBus.EmoteSlotSelectEvent += OnEmoteSlotSelected;
            backpackEventBus.UnEquipEmoteEvent += OnEmoteUnEquipped;
            backpackEventBus.FilterCategoryByEnumEvent += OnChangeCategory;
            backpackEventBus.ForceRenderEvent += OnForceRenderChange;
            backpackEventBus.ChangedBackpackSectionEvent += OnBackpackSectionChanged;
            backpackEventBus.DeactivateEvent += OnDeactivate;
        }

        private void OnDeactivate()
        {
            StopEmotes();
        }

        private void OnBackpackSectionChanged(BackpackSections backpackSection)
        {
            switch (backpackSection)
            {
                case BackpackSections.Avatar:
                    rotateEnabled = true;
                    panEnabled = true;
                    zoomEnabled = true;
                    StopEmotes();
                    break;
                case BackpackSections.Emotes:
                    inputEventBus.OnChangePreviewFocus(AvatarWearableCategoryEnum.Body);
                    rotateEnabled = true;
                    panEnabled = false;
                    zoomEnabled = false;
                    break;
            }
        }

        public new void Dispose()
        {
            base.Dispose();

            backpackEventBus.EquipWearableEvent -= OnWearableEquipped;
            backpackEventBus.UnEquipWearableEvent -= OnWearableUnequipped;
            backpackEventBus.EquipEmoteEvent -= OnEmoteEquipped;
            backpackEventBus.UnEquipEmoteEvent -= OnEmoteUnEquipped;
            backpackEventBus.SelectEmoteEvent -= OnEmoteSelected;
            backpackEventBus.EmoteSlotSelectEvent -= OnEmoteSlotSelected;
            backpackEventBus.FilterCategoryByEnumEvent -= OnChangeCategory;
            backpackEventBus.ForceRenderEvent -= OnForceRenderChange;
            backpackEventBus.ChangedBackpackSectionEvent -= OnBackpackSectionChanged;
        }

        private void OnChangeCategory(AvatarWearableCategoryEnum categoryEnum)
        {
            inputEventBus.OnChangePreviewFocus(categoryEnum);
        }

        private void OnForceRenderChange(IReadOnlyCollection<string> forceRender)
        {
            previewAvatarModel.ForceRenderCategories.Clear();

            foreach (string wearable in forceRender) { previewAvatarModel.ForceRenderCategories.Add(wearable); }

            OnModelUpdated();
        }

        private void OnWearableEquipped(IWearable i)
        {
            previewAvatarModel.Wearables ??= new List<URN>();

            if (i.Type == WearableType.BodyShape)
                previewAvatarModel.BodyShape = i.GetUrn();
            else previewAvatarModel.Wearables.Add(i.GetUrn());

            OnModelUpdated();
        }

        private void OnWearableUnequipped(IWearable i)
        {
            previewAvatarModel.Wearables.Remove(i.GetUrn());
            OnModelUpdated();
        }

        private void OnEmoteUnEquipped(int slot, IEmote? emote)
        {
            if (emote == null) return;
            if (previewAvatarModel.Emotes == null) return;
            previewAvatarModel.Emotes.Remove(emote.GetUrn());
            OnModelUpdated();
        }

        private void OnEmoteEquipped(int slot, IEmote emote)
        {
            previewAvatarModel.Emotes ??= new HashSet<URN>();

            URN urn = emote.GetUrn().Shorten();
            if (previewAvatarModel.Emotes.Contains(urn)) return;
            previewAvatarModel.Emotes.Add(urn);

            OnModelUpdated();
        }

        private void OnEmoteSlotSelected(int slot)
        {
            IEmote? emote = equippedEmotes.EmoteInSlot(slot);
            if (emote == null) return;
            PlayEmote(emote.GetUrn().Shorten());
        }

        private void OnEmoteSelected(IEmote emote)
        {
            async UniTaskVoid EnsureEmoteAndPlayItAsync(CancellationToken ct)
            {
                URN urn = emote.GetUrn().Shorten();

                // In case you want to preview an emote, it might happen that the asset bundles are not loaded
                // By adding the emote we force to fetch them if missing
                if (!previewAvatarModel.Emotes?.Contains(urn) ?? true)
                {
                    previewAvatarModel.Emotes!.Add(urn);
                    await UpdateAvatarAsync(previewAvatarModel, ct);
                    // Remove the emote so it stays original
                    previewAvatarModel.Emotes!.Remove(urn);
                }

                PlayEmote(urn);
            }

            EnsureEmoteAndPlayItAsync(CancellationToken.None).Forget();
        }
    }
}
