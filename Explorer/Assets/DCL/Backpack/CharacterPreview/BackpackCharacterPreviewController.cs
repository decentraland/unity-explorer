﻿using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
using DCL.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Backpack.CharacterPreview
{
    public class BackpackCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IEquippedEmotes equippedEmotes;
        private CancellationTokenSource? emotePreviewCancellationToken;

        public BackpackCharacterPreviewController(CharacterPreviewView view,
            ICharacterPreviewFactory previewFactory,
            IBackpackEventBus backpackEventBus,
            World world,
            IEquippedEmotes equippedEmotes,
            CharacterPreviewEventBus characterPreviewEventBus)
            : base(view, previewFactory, world, true, characterPreviewEventBus)
        {
            this.backpackEventBus = backpackEventBus;
            this.equippedEmotes = equippedEmotes;
            backpackEventBus.EquipWearableEvent += OnWearableEquipped;
            backpackEventBus.ChangeColorEvent += OnColorChange;
            backpackEventBus.UnEquipWearableEvent += OnWearableUnequipped;
            backpackEventBus.UnEquipAllEvent += UnEquipAll;
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

            emotePreviewCancellationToken.SafeCancelAndDispose();
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
            backpackEventBus.ChangeColorEvent -= OnColorChange;
            backpackEventBus.UnEquipWearableEvent -= OnWearableUnequipped;
            backpackEventBus.EquipEmoteEvent -= OnEmoteEquipped;
            backpackEventBus.UnEquipEmoteEvent -= OnEmoteUnEquipped;
            backpackEventBus.SelectEmoteEvent -= OnEmoteSelected;
            backpackEventBus.EmoteSlotSelectEvent -= OnEmoteSlotSelected;
            backpackEventBus.FilterCategoryByEnumEvent -= OnChangeCategory;
            backpackEventBus.ForceRenderEvent -= OnForceRenderChange;
            backpackEventBus.ChangedBackpackSectionEvent -= OnBackpackSectionChanged;
            backpackEventBus.UnEquipAllEvent -= UnEquipAll;

            emotePreviewCancellationToken.SafeCancelAndDispose();
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

        private void OnColorChange(Color newColor, string category)
        {
            switch (category)
            {
                case WearableCategories.Categories.EYES:
                    previewAvatarModel.EyesColor = newColor;
                    break;
                case WearableCategories.Categories.HAIR:
                    previewAvatarModel.HairColor = newColor;
                    break;
                case WearableCategories.Categories.BODY_SHAPE:
                    previewAvatarModel.SkinColor = newColor;
                    break;
            }

            OnModelUpdated();
        }

        private void OnWearableUnequipped(IWearable i)
        {
            previewAvatarModel.Wearables.Remove(i.GetUrn());
            OnModelUpdated();
        }

        private void UnEquipAll()
        {
            previewAvatarModel.Wearables?.Clear();
        }

        private void OnEmoteUnEquipped(int slot, IEmote? emote)
        {
            if (emote == null) return;
            if (previewAvatarModel.Emotes == null) return;
            previewAvatarModel.Emotes.Remove(emote.GetUrn());
            OnModelUpdated();
        }

        private void OnEmoteEquipped(int slot, IEmote emote, bool isManuallyEquipped)
        {
            previewAvatarModel.Emotes ??= new HashSet<URN>();

            URN urn = emote.GetUrn().Shorten();
            if (!previewAvatarModel.Emotes.Add(urn))
                return;

            OnModelUpdated();

            if (isManuallyEquipped)
                PlayEmote(urn);
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

                // Ensure assets are loaded for the emote
                if (!previewAvatarModel.Emotes?.Contains(urn) ?? true)
                {
                    previewAvatarModel.Emotes!.Add(urn);

                    try { await ShowLoadingSpinnerAndUpdateAvatarAsync(ct); }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        // Remove the emote so it stays original
                        previewAvatarModel.Emotes!.Remove(urn);
                    }
                }

                PlayEmote(urn);
            }

            emotePreviewCancellationToken = emotePreviewCancellationToken.SafeRestart();
            EnsureEmoteAndPlayItAsync(emotePreviewCancellationToken.Token).Forget();
        }
    }
}
