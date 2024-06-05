using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Backpack
{
    public class BackpackSlotsController : IDisposable
    {
        private const int MAX_HIDES = 15;

        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackEventBus backpackEventBus;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly Dictionary<string, (AvatarSlotView, CancellationTokenSource)> avatarSlots = new ();
        private readonly List<IWearable> equippedWearables = new (MAX_HIDES);
        private readonly HashSet<string> forceRender = new (MAX_HIDES);
        private readonly HashSet<string> hidingList = new (MAX_HIDES);

        private AvatarSlotView previousSlot;

        public BackpackSlotsController(
            AvatarSlotView[] avatarSlotViews,
            BackpackCommandBus backpackCommandBus,
            BackpackEventBus backpackEventBus,
            NftTypeIconSO rarityBackgrounds,
            IThumbnailProvider thumbnailProvider)
        {
            this.backpackCommandBus = backpackCommandBus;
            this.backpackEventBus = backpackEventBus;
            this.rarityBackgrounds = rarityBackgrounds;
            this.thumbnailProvider = thumbnailProvider;

            this.backpackEventBus.EquipWearableEvent += EquipInSlot;
            this.backpackEventBus.UnEquipWearableEvent += UnEquipInSlot;
            this.backpackEventBus.FilterCategoryEvent += DeselectCategory;
            this.backpackEventBus.ForceRenderEvent += SetForceRender;
            this.backpackEventBus.UnEquipAllEvent += UnEquipAll;

            foreach (var avatarSlotView in avatarSlotViews)
            {
                avatarSlots.Add(avatarSlotView.Category.ToLower(), (avatarSlotView, new CancellationTokenSource()));
                avatarSlotView.OnSlotButtonPressed += OnSlotButtonPressed;
                avatarSlotView.OverrideHide.onClick.AddListener(() => RemoveForceRender(avatarSlotView.Category));
                avatarSlotView.NoOverride.onClick.AddListener(() => AddForceRender(avatarSlotView.Category));
                avatarSlotView.UnequipButton.onClick.AddListener(() => backpackCommandBus.SendCommand(new BackpackUnEquipWearableCommand(avatarSlotView.SlotWearableUrn)));
            }
        }

        private void SetForceRender(IReadOnlyCollection<string> forceRenders, bool isInitialHide)
        {
            forceRender.Clear();

            foreach (string render in forceRenders)
                forceRender.Add(render);

            CalculateHideStatus();
        }

        private void DeselectCategory(string filterContent)
        {
            if (previousSlot != null && string.IsNullOrEmpty(filterContent))
            {
                previousSlot.SelectedBackground.SetActive(false);
                previousSlot = null;
            }
        }

        private void UnEquipInSlot(IWearable wearable)
        {
            if (!avatarSlots.TryGetValue(wearable.GetCategory(), out (AvatarSlotView, CancellationTokenSource) avatarSlotView)) return;

            equippedWearables.Remove(wearable);

            avatarSlotView.Item2.SafeCancelAndDispose();
            avatarSlotView.Item1.UnequipButton.gameObject.SetActive(false);
            avatarSlotView.Item1.SlotWearableUrn = null;
            avatarSlotView.Item1.SlotWearableThumbnail.gameObject.SetActive(false);
            avatarSlotView.Item1.SlotWearableThumbnail.sprite = null;
            avatarSlotView.Item1.SlotWearableRarityBackground.sprite = null;
            avatarSlotView.Item1.EmptyOverlay.SetActive(true);

            CalculateHideStatus();
        }

        private void UnEquipAll()
        {
            equippedWearables.Clear();

            foreach ((string? key, (AvatarSlotView, CancellationTokenSource) value) in avatarSlots)
            {
                value.Item2.SafeCancelAndDispose();
                value.Item1.UnequipButton.gameObject.SetActive(false);
                value.Item1.SlotWearableUrn = null;
                value.Item1.SlotWearableThumbnail.gameObject.SetActive(false);
                value.Item1.SlotWearableThumbnail.sprite = null;
                value.Item1.SlotWearableRarityBackground.sprite = null;
                value.Item1.EmptyOverlay.SetActive(true);
            }

            forceRender.Clear();
        }

        private void EquipInSlot(IWearable equippedWearable, bool isInitialEquip)
        {
            if (!avatarSlots.TryGetValue(equippedWearable.GetCategory(), out (AvatarSlotView, CancellationTokenSource) avatarSlotView))
                return;

            equippedWearables.Add(equippedWearable);

            avatarSlotView.Item1.SlotWearableUrn = equippedWearable.GetUrn();
            avatarSlotView.Item1.SlotWearableRarityBackground.sprite = rarityBackgrounds.GetTypeImage(equippedWearable.GetRarity());
            avatarSlotView.Item1.EmptyOverlay.SetActive(false);

            avatarSlotView.Item2.SafeCancelAndDispose();
            avatarSlotView.Item2 = new CancellationTokenSource();

            CalculateHideStatus();

            WaitForThumbnailAsync(equippedWearable, avatarSlotView.Item1, avatarSlotView.Item2.Token).Forget();
        }

        private void CalculateHideStatus()
        {
            AvatarWearableHide.ComposeHiddenCategoriesOrdered(avatarSlots["body_shape"].Item1.SlotWearableUrn, null, equippedWearables, hidingList);

            foreach (var avatarSlotView in avatarSlots.Values)
            {
                avatarSlotView.Item1.OverrideHideContainer.SetActive(false);

                string hiderTextText = AvatarWearableHide.GetCategoryHider(avatarSlots["body_shape"].Item1.SlotWearableUrn, avatarSlotView.Item1.Category, equippedWearables);
                avatarSlotView.Item1.HiderText.gameObject.SetActive(!string.IsNullOrEmpty(hiderTextText));

                if(hiderTextText != null && AvatarWearableHide.CATEGORIES_TO_READABLE.TryGetValue(hiderTextText, out string readableCategoryHider))
                    avatarSlotView.Item1.HiderText.text = $"Hidden by <b>{readableCategoryHider}</b>";
            }

            foreach (string category in hidingList)
            {
                if (avatarSlots.TryGetValue(category, out (AvatarSlotView, CancellationTokenSource) avatarSlotViewToHide))
                {
                    avatarSlotViewToHide.Item1.OverrideHideContainer.SetActive(!string.IsNullOrEmpty(avatarSlotViewToHide.Item1.SlotWearableUrn));
                    avatarSlotViewToHide.Item1.OverrideHide.gameObject.SetActive(forceRender.Contains(category));
                    avatarSlotViewToHide.Item1.NoOverride.gameObject.SetActive(!forceRender.Contains(category));
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

            Sprite? thumbnail = await thumbnailProvider.GetAsync(equippedWearable, ct);

            avatarSlots[equippedWearable.GetCategory()].Item1.SlotWearableThumbnail.sprite = thumbnail;
            avatarSlots[equippedWearable.GetCategory()].Item1.SlotWearableThumbnail.gameObject.SetActive(true);
            avatarSlotView.LoadingView.FinishLoadingAnimation(avatarSlotView.NftContainer);
        }

        private void OnSlotButtonPressed(AvatarSlotView avatarSlot)
        {
            if (previousSlot != null)
                previousSlot.SelectedBackground.SetActive(false);

            if (avatarSlot == previousSlot)
            {
                previousSlot.SelectedBackground.SetActive(false);
                backpackCommandBus.SendCommand(new BackpackFilterCategoryCommand(""));
                previousSlot = null;
                return;
            }

            previousSlot = avatarSlot;
            backpackCommandBus.SendCommand(new BackpackFilterCategoryCommand(avatarSlot.Category, avatarSlot.CategoryEnum));
            avatarSlot.SelectedBackground.SetActive(true);
        }

        public void Dispose()
        {
            backpackEventBus.EquipWearableEvent -= EquipInSlot;
            backpackEventBus.UnEquipWearableEvent -= UnEquipInSlot;
            this.backpackEventBus.UnEquipAllEvent -= UnEquipAll;
            foreach (var avatarSlotView in avatarSlots.Values)
                avatarSlotView.Item1.OnSlotButtonPressed -= OnSlotButtonPressed;
        }
    }
}
