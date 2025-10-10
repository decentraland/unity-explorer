using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.AvatarSection.Outfits.Repository;
using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Backpack.AvatarSection.Outfits.Slots;
using DCL.Diagnostics;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Backpack.Slots
{
    public class OutfitData
    {
        public string BodyShapeUrn { get; set; }
        public List<string> WearableUrns { get; set; }
        public Color SkinColor { get; set; }
        public Color HairColor { get; set; }
        public Color EyesColor { get; set; }
        public string ThumbnailUrl { get; set; }

        public override string ToString()
        {
            return $"BodyShape: {BodyShapeUrn}, Wearables: [{string.Join(", ", WearableUrns)}], " +
                   $"SkinColor: {SkinColor}, HairColor: {HairColor}, EyesColor: {EyesColor}, ThumbnailUrl: {ThumbnailUrl}";
        }
    }

    public enum OutfitSlotState { Empty, Loading, Full, Save }

    public class OutfitSlotPresenter : IDisposable
    {
        private readonly OutfitsController ownerController;
        private readonly HoverHandler hoverHandler;
        private readonly IAvatarScreenshotService screenshotService;
        private CancellationTokenSource cts = new ();
        
        private bool isHovered;
        private OutfitSlotState currentState;
        private OutfitItem? currentOutfitData;
        private Texture2D? currentThumbnail;
        private readonly Sprite popupDeleteIcon;

        public readonly OutfitSlotView view;
        public readonly int slotIndex;

        public OutfitSlotPresenter(
            Sprite popupDeleteIcon,
            OutfitSlotView view,
            int slotIndex,
            OutfitsController ownerController,
            IAvatarScreenshotService screenshotService)
        {
            this.popupDeleteIcon = popupDeleteIcon;
            this.view = view;
            this.slotIndex = slotIndex;
            this.ownerController = ownerController;
            this.screenshotService = screenshotService;

            view.OnSaveClicked += HandleSaveClicked;
            view.OnDeleteClicked += HandleDeleteClicked;
            view.OnEquipClicked += HandleEquipClicked;
            view.OnUnEquipClicked += HandleUnequipClicked;
            
            hoverHandler = view.hoverHandler;
            hoverHandler.OnHoverEntered += OnHoverEntered;
            hoverHandler.OnHoverExited += OnHoverExited;
        }

        private void HandleSaveClicked()
        {
            // HandleSaveClickedAsync().Forget();
            ownerController.OnSaveOutfitRequested(slotIndex);
        }

        private void HandleDeleteClicked()
        {
            // HandleDeleteClickedAsync().Forget();
            ownerController.OnDeleteOutfitRequested(slotIndex);
        }

        private void OnHoverEntered()
        {
            isHovered = true;
            UpdateView();
        }

        private void OnHoverExited()
        {
            isHovered = false;
            UpdateView();
        }

        public void SetData(OutfitItem item)
        {
            currentOutfitData = item;
            SetState(OutfitSlotState.Full, item);
            LoadExistingScreenshotAsync().Forget();
        }

        public void SetEmpty()
        {
            if (currentThumbnail != null)
            {
                Object.Destroy(currentThumbnail);
                currentThumbnail = null;
            }
            SetState(OutfitSlotState.Empty);
        }

        public void SetSaving()
        {
            SetState(OutfitSlotState.Save);
        }

        public void SetLoading()
        {
            SetState(OutfitSlotState.Loading);
        }

        private void SetState(OutfitSlotState newState, OutfitItem? item = null)
        {
            currentState = newState;
            currentOutfitData = item;
            UpdateView();
        }

        private void UpdateView()
        {
            switch (currentState)
            {
                case OutfitSlotState.Empty: view.ShowEmptyState(isHovered); break;
                case OutfitSlotState.Loading: view.ShowLoadingState(); break;
                case OutfitSlotState.Save: view.ShowStateSaving(); break;
                case OutfitSlotState.Full:

                    view.ShowFullState(currentThumbnail, isHovered);
                    break;
            }
        }

        private void HandleEquipClicked()
        {
            ReportHub.Log(ReportCategory.OUTFITS, "Equip outfit clicked");
            ownerController.OnEquipOutfitRequested(currentOutfitData);
        }

        private void HandleUnequipClicked()
        {
            ReportHub.Log(ReportCategory.OUTFITS, "Unequip outfit clicked");
        }

        private async UniTaskVoid LoadExistingScreenshotAsync()
        {
            if (currentOutfitData == null) return;

            try
            {
                // Asynchronously load the texture from the disk using the service
                var loadedTexture = await screenshotService.LoadScreenshotAsync(slotIndex, cts.Token);
                if (cts.IsCancellationRequested) return;

                SetThumbnail(loadedTexture);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                SetThumbnail(null); // Ensure UI updates even if loading fails
            }
        }

        public void SetThumbnail(Texture2D? screenshot)
        {
            if (currentThumbnail != null)
                Object.Destroy(currentThumbnail);

            currentThumbnail = screenshot;
            UpdateView();
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
            view.OnSaveClicked -= HandleSaveClicked;
            view.OnEquipClicked -= HandleEquipClicked;
            view.OnUnEquipClicked -= HandleUnequipClicked;
            view.OnDeleteClicked -= HandleDeleteClicked;

            hoverHandler.OnHoverEntered -= OnHoverEntered;
            hoverHandler.OnHoverExited -= OnHoverExited;

            if (currentThumbnail != null)
            {
                Object.Destroy(currentThumbnail);
                currentThumbnail = null;
            }
        }

        public OutfitItem GetOutfitData()
        {
            return currentOutfitData!;
        }
    }
}