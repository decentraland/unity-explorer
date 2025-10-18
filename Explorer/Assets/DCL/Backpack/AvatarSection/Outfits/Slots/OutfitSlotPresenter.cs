using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.AvatarSection.Outfits.Models;
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

    public enum OutfitSlotState { Empty, Loading, Full, Save, EmptyAndReadyToSave }

    public class OutfitSlotPresenter : IDisposable
    {
        public event Action<int> OnSaveRequested;
        public event Action<int> OnDeleteRequested;
        public event Action<OutfitItem> OnEquipRequested;
        public event Action<OutfitItem> OnPreviewRequested;

        public bool HasThumbnail()
        {
            return currentThumbnail != null;
        }

        public readonly OutfitSlotView view;
        public readonly int slotIndex;
        
        private readonly HoverHandler hoverHandler;
        private readonly IAvatarScreenshotService screenshotService;
        private CancellationTokenSource cts = new ();
        
        private bool isHovered;
        private bool isForcedHover;
        private OutfitSlotState currentState;
        private OutfitItem? currentOutfitData;
        private Texture2D? currentThumbnail;

        public OutfitSlotPresenter( OutfitSlotView view,
            int slotIndex,
            IAvatarScreenshotService screenshotService)
        {
            this.view = view;
            this.slotIndex = slotIndex;
            this.screenshotService = screenshotService;

            view.OnSaveClicked += HandleSaveClicked;
            view.OnDeleteClicked += HandleDeleteClicked;
            view.OnEquipClicked += HandleEquipClicked;
            view.OnPreviewClicked += HandlePreviewClicked;
            
            hoverHandler = view.hoverHandler;
            hoverHandler.OnHoverEntered += OnHoverEntered;
            hoverHandler.OnHoverExited += OnHoverExited;
        }

        public OutfitItem GetOutfitData()
        {
            return currentOutfitData!;
        }

        private void HandleSaveClicked()
        {
            OnSaveRequested?.Invoke(slotIndex);
        }

        private void HandleDeleteClicked()
        {
            OnDeleteRequested?.Invoke(slotIndex);
        }

        private void HandlePreviewClicked()
        {
            OnPreviewRequested?.Invoke(currentOutfitData!);
        }

        private void HandleEquipClicked()
        {
            OnEquipRequested?.Invoke(currentOutfitData!);
        }

        private void OnHoverEntered()
        {
            isHovered = true;
            UpdateView();
            view.AnimateHover();
        }

        private void OnHoverExited()
        {
            isHovered = false;
            UpdateView();
            view.AnimateExit();
        }

        public void SetData(OutfitItem item, bool loadThumbnail = true)
        {
            currentOutfitData = item;
            SetState(OutfitSlotState.Full, item);
            isForcedHover = false;

            if (loadThumbnail)
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
            isForcedHover = false;
        }

        public void SetSaving()
        {
            SetState(OutfitSlotState.Save);
            isForcedHover = false;
        }

        public void SetLoading()
        {
            SetState(OutfitSlotState.Loading);
            isForcedHover = false;
        }

        public bool IsEmpty()
        {
            return currentState == OutfitSlotState.Empty ||
                   currentState == OutfitSlotState.EmptyAndReadyToSave;
        }

        public void SetAsFirstEmptyAndReadyToSave(bool active)
        {
            isForcedHover = active;
            if (active)
                SetState(OutfitSlotState.EmptyAndReadyToSave);
            else if (currentState == OutfitSlotState.EmptyAndReadyToSave)
                SetState(OutfitSlotState.Empty);

            UpdateView();
        }

        public void SetEquipped(bool equipped)
        {
            ReportHub.Log(ReportCategory.OUTFITS, $"SetEquipped {equipped} in slot {slotIndex}");
            view.SetEquipped(equipped);
        }

        private void SetState(OutfitSlotState newState, OutfitItem? item = null)
        {
            currentState = newState;
            currentOutfitData = item;
            UpdateView();
        }

        private void UpdateView()
        {
            bool effectiveHover = isHovered || isForcedHover; 
            
            switch (currentState)
            {
                case OutfitSlotState.Empty: view.ShowEmptyState(isHovered); break;
                case OutfitSlotState.EmptyAndReadyToSave:
                    view.ShowEmptyState(effectiveHover);
                    break;
                case OutfitSlotState.Loading: view.ShowLoadingState(); break;
                case OutfitSlotState.Save: view.ShowStateSaving(); break;
                case OutfitSlotState.Full:

                    view.ShowFullState(currentThumbnail, effectiveHover);
                    break;
            }
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
                SetThumbnail(null);
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
            view.OnDeleteClicked -= HandleDeleteClicked;
            view.OnPreviewClicked -= HandlePreviewClicked;

            hoverHandler.OnHoverEntered -= OnHoverEntered;
            hoverHandler.OnHoverExited -= OnHoverExited;

            if (currentThumbnail != null)
            {
                Object.Destroy(currentThumbnail);
                currentThumbnail = null;
            }
        }
    }
}