using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Backpack.AvatarSection.Outfits.Commands;
using UnityEngine;
using Utility;

namespace DCL.Backpack
{
    public class BackpackSlotsController : IDisposable
    {
        private const int MAX_HIDES = 15;

        private readonly BackpackCommandBus backpackCommandBus;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly Dictionary<string, AvatarSlot> avatarSlots = new ();
        private readonly List<IWearable> equippedWearables = new (MAX_HIDES);
        private readonly HashSet<string> forceRender = new (MAX_HIDES);
        private readonly HashSet<string> hidingList = new (MAX_HIDES);

        private AvatarSlotView previousSlot;

        public BackpackSlotsController(
            AvatarSlotView[] avatarSlotViews,
            BackpackCommandBus backpackCommandBus,
            IBackpackEventBus backpackEventBus,
            NftTypeIconSO rarityBackgrounds,
            IThumbnailProvider thumbnailProvider)
        {
            this.backpackCommandBus = backpackCommandBus;
            this.backpackEventBus = backpackEventBus;
            this.rarityBackgrounds = rarityBackgrounds;
            this.thumbnailProvider = thumbnailProvider;

            this.backpackEventBus.EquipWearableEvent += EquipInSlot;
            this.backpackEventBus.UnEquipWearableEvent += UnEquipInSlot;
            this.backpackEventBus.FilterEvent += OnFilterEvent;
            this.backpackEventBus.ForceRenderEvent += SetForceRender;
            this.backpackEventBus.UnEquipAllEvent += UnEquipAll;
            this.backpackEventBus.UnEquipAllWearablesEvent += UnEquipAll;
            this.backpackEventBus.EquipOutfitEvent += OnEquipOutfit;

            foreach (var avatarSlotView in avatarSlotViews)
            {
                avatarSlots.Add(avatarSlotView.Category.ToLower(), new AvatarSlot(avatarSlotView));
                avatarSlotView.OnSlotButtonPressed += OnSlotButtonPressed;
                avatarSlotView.OverrideHide.onClick.AddListener(() => RemoveForceRender(avatarSlotView.Category));
                avatarSlotView.NoOverride.onClick.AddListener(() => AddForceRender(avatarSlotView.Category));
                avatarSlotView.UnequipButton.onClick.AddListener(() => backpackCommandBus.SendCommand(new BackpackUnEquipWearableCommand(avatarSlotView.SlotWearableUrn)));
            }
        }

        private void SetForceRender(IReadOnlyCollection<string> forceRenders)
        {
            forceRender.Clear();

            foreach (string render in forceRenders)
                forceRender.Add(render);

            CalculateHideStatus();
        }

        private void OnFilterEvent(string? category, AvatarWearableCategoryEnum? categoryEnum, string? searchText)
        {
            if (previousSlot != null && string.IsNullOrEmpty(category))
            {
                previousSlot.SelectedBackground.SetActive(false);
                previousSlot = null;
            }
        }

        private void UnEquipInSlot(IWearable wearable)
        {
            if (!avatarSlots.TryGetValue(wearable.GetCategory(), out AvatarSlot avatarSlot)) return;

            equippedWearables.Remove(wearable);

            avatarSlot.CancelThumbnailLoad();
            AvatarSlotView avatarSlotView = avatarSlot.View;
            avatarSlotView.UnequipButton.gameObject.SetActive(false);
            avatarSlotView.SlotWearableUrn = null;
            avatarSlotView.SlotWearableThumbnail.gameObject.SetActive(false);
            avatarSlotView.SlotWearableThumbnail.sprite = null;
            avatarSlotView.SlotWearableRarityBackground.sprite = null;
            avatarSlotView.EmptyOverlay.SetActive(true);

            CalculateHideStatus();
        }

        private void UnEquipAll()
        {
            equippedWearables.Clear();

            foreach (AvatarSlot avatarSlot in avatarSlots.Values)
            {
                avatarSlot.CancelThumbnailLoad();
                AvatarSlotView avatarSlotView = avatarSlot.View;
                avatarSlotView.UnequipButton.gameObject.SetActive(false);
                avatarSlotView.SlotWearableUrn = null;
                avatarSlotView.SlotWearableThumbnail.gameObject.SetActive(false);
                avatarSlotView.SlotWearableThumbnail.sprite = null;
                avatarSlotView.SlotWearableRarityBackground.sprite = null;
                avatarSlotView.EmptyOverlay.SetActive(true);
            }

            forceRender.Clear();
        }

        private void EquipInSlot(IWearable equippedWearable, bool isManuallyEquipped)
        {
            if (!avatarSlots.TryGetValue(equippedWearable.GetCategory(), out AvatarSlot avatarSlot))
                return;

            equippedWearables.Add(equippedWearable);

            avatarSlot.View.SlotWearableUrn = equippedWearable.GetUrn();
            avatarSlot.View.SlotWearableRarityBackground.sprite = rarityBackgrounds.GetTypeImage(equippedWearable.GetRarity());
            avatarSlot.View.EmptyOverlay.SetActive(false);

            CancellationToken ct = avatarSlot.RestartThumbnailLoad();

            CalculateHideStatus();

            WaitForThumbnailAsync(equippedWearable, avatarSlot.View, ct).Forget();
        }

        private void CalculateHideStatus()
        {
            WearableComponentsUtils.ComposeHiddenCategoriesOrdered(avatarSlots["body_shape"].View.SlotWearableUrn, null, equippedWearables, hidingList);

            foreach (AvatarSlot avatarSlot in avatarSlots.Values)
            {
                AvatarSlotView avatarSlotView = avatarSlot.View;
                avatarSlotView.OverrideHideContainer.SetActive(false);

                string hiderTextText = WearableComponentsUtils.GetCategoryHider(avatarSlots["body_shape"].View.SlotWearableUrn, avatarSlotView.Category, equippedWearables);
                avatarSlotView.HiderText.gameObject.SetActive(!string.IsNullOrEmpty(hiderTextText));

                if (!string.IsNullOrEmpty(hiderTextText) && WearableComponentsUtils.CATEGORIES_TO_READABLE.TryGetValue(hiderTextText, out string readableCategoryHider))
                    avatarSlotView.HiderText.text = $"Hidden by <b>{readableCategoryHider}</b>";
            }

            foreach (string category in hidingList)
            {
                if (avatarSlots.TryGetValue(category, out AvatarSlot avatarSlotToHide))
                {
                    AvatarSlotView avatarSlotViewToHide = avatarSlotToHide.View;
                    avatarSlotViewToHide.OverrideHideContainer.SetActive(!string.IsNullOrEmpty(avatarSlotViewToHide.SlotWearableUrn));
                    avatarSlotViewToHide.OverrideHide.gameObject.SetActive(forceRender.Contains(category));
                    avatarSlotViewToHide.NoOverride.gameObject.SetActive(!forceRender.Contains(category));
                }
            }
        }

        private void RemoveForceRender(string category)
        {
            forceRender.Remove(category.ToLower());
            backpackCommandBus.SendCommand(new BackpackHideCommand(forceRender));
        }

        private void AddForceRender(string category)
        {
            forceRender.Add(category.ToLower());
            backpackCommandBus.SendCommand(new BackpackHideCommand(forceRender));
        }

        private async UniTaskVoid WaitForThumbnailAsync(IWearable equippedWearable, AvatarSlotView avatarSlotView, CancellationToken ct)
        {
            avatarSlotView.LoadingView.StartLoadingAnimation(avatarSlotView.NftContainer);

            try
            {
                Sprite thumbnail = await thumbnailProvider.GetAsync(equippedWearable, ct);

                if (ct.IsCancellationRequested) return;

                avatarSlotView.SlotWearableThumbnail.sprite = thumbnail;
                avatarSlotView.SlotWearableThumbnail.gameObject.SetActive(true);
                avatarSlotView.LoadingView.FinishLoadingAnimation(avatarSlotView.NftContainer);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.THUMBNAILS)); }
        }

        private void OnSlotButtonPressed(AvatarSlotView avatarSlot)
        {
            if (previousSlot != null)
                previousSlot.SelectedBackground.SetActive(false);

            if (avatarSlot == previousSlot)
            {
                previousSlot.SelectedBackground.SetActive(false);
                backpackCommandBus.SendCommand(new BackpackFilterCommand(string.Empty, AvatarWearableCategoryEnum.Body, string.Empty));
                previousSlot = null;
                return;
            }

            previousSlot = avatarSlot;
            backpackCommandBus.SendCommand(new BackpackFilterCommand(avatarSlot.Category, avatarSlot.CategoryEnum, string.Empty));
            avatarSlot.SelectedBackground.SetActive(true);
        }

        private void OnEquipOutfit(BackpackEquipOutfitCommand command, IReadOnlyCollection<IWearable> wearables)
        {
            UnEquipAll();

            foreach (var w in wearables)
            {
                if (!avatarSlots.TryGetValue(w.GetCategory(), out AvatarSlot avatarSlot))
                    continue;

                equippedWearables.Add(w);

                avatarSlot.View.SlotWearableUrn = w.GetUrn();
                avatarSlot.View.SlotWearableRarityBackground.sprite = rarityBackgrounds.GetTypeImage(w.GetRarity());
                avatarSlot.View.EmptyOverlay.SetActive(false);

                WaitForThumbnailAsync(w, avatarSlot.View, avatarSlot.RestartThumbnailLoad()).Forget();
            }

            forceRender.Clear();
            foreach (var f in command.ForceRender)
                forceRender.Add(f);

            CalculateHideStatus();
        }

        public void Dispose()
        {
            backpackEventBus.EquipWearableEvent -= EquipInSlot;
            backpackEventBus.UnEquipWearableEvent -= UnEquipInSlot;
            backpackEventBus.UnEquipAllEvent -= UnEquipAll;
            backpackEventBus.UnEquipAllWearablesEvent -= UnEquipAll;
            backpackEventBus.EquipOutfitEvent -= OnEquipOutfit;

            foreach (AvatarSlot avatarSlot in avatarSlots.Values)
            {
                avatarSlot.View.OnSlotButtonPressed -= OnSlotButtonPressed;
                avatarSlot.CancelThumbnailLoad();
            }
        }

        private sealed class AvatarSlot
        {
            public readonly AvatarSlotView View;

            private CancellationTokenSource? thumbnailCts;

            public AvatarSlot(AvatarSlotView view)
            {
                View = view;
            }

            public CancellationToken RestartThumbnailLoad()
            {
                thumbnailCts = thumbnailCts.SafeRestart();
                return thumbnailCts.Token;
            }

            public void CancelThumbnailLoad()
            {
                thumbnailCts.SafeCancelAndDispose();
                thumbnailCts = null;
            }
        }
    }
}
