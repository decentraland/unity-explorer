using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Backpack.AvatarSection.Outfits.Slots;
using DCL.Backpack.BackpackBus;
using DCL.Diagnostics;
using DCL.UI.ConfirmationDialog.Opener;
using DCL.Utilities.Extensions;
using MVC;
using UnityEngine;
using Utility;

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
    }

    public enum OutfitSlotState { Empty, Loading, Full, Save }

    public class OutfitSlotPresenter : IDisposable
    {
        public string OUTFIT_POPUP_DELETE_TEXT = "Are you sure you want to delete this Outfit?";
        public string OUTFIT_POPUP_DELETE_CANCEL_TEXT = "CANCEL";
        public string OUTFIT_POPUP_DELETE_CONFIRM_TEXT = "YES";
        
        public readonly OutfitSlotView view;
        public readonly int slotIndex;
        private readonly IOutfitsService outfitsAPI;
        private readonly BackpackCommandBus commandBus;

        private CancellationTokenSource cts = new ();
        private readonly HoverHandler hoverHandler;
        private bool isHovered;
        private OutfitSlotState currentState;
        private OutfitData? currentOutfitData;
        private readonly Sprite popupDeleteIcon;

        public OutfitSlotPresenter(
            Sprite popupDeleteIcon,
            OutfitSlotView view,
            int slotIndex,
            IOutfitsService outfitsAPI,
            BackpackCommandBus commandBus)
        {
            this.popupDeleteIcon = popupDeleteIcon;
            this.view = view;
            this.slotIndex = slotIndex;
            this.outfitsAPI = outfitsAPI;
            this.commandBus = commandBus;

            view.OnSaveClicked += HandleSaveClicked;
            view.OnEquipClicked += HandleEquipClicked;
            view.OnUnEquipClicked += HandleUnequipClicked;
            view.OnDeleteClicked += HandleDeleteClicked;
            hoverHandler = view.hoverHandler;
            hoverHandler.OnHoverEntered += OnHoverEntered;
            hoverHandler.OnHoverExited += OnHoverExited;
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

        public void SetData(OutfitData data)
        {
            SetState(OutfitSlotState.Full, data);
        }

        public void SetEmpty()
        {
            SetState(OutfitSlotState.Empty);
        }

        public void SetLoading()
        {
            SetState(OutfitSlotState.Loading);
        }

        private void SetState(OutfitSlotState newState, OutfitData? data = null)
        {
            currentState = newState;
            currentOutfitData = data;
            UpdateView();
        }

        private void UpdateView()
        {
            switch (currentState)
            {
                case OutfitSlotState.Empty:
                    view.ShowEmptyState(isHovered);
                    break;
                case OutfitSlotState.Loading:
                    view.ShowLoadingState();
                    break;
                case OutfitSlotState.Save:
                    view.ShowStateSaving();
                    break;
                case OutfitSlotState.Full:

                    // TODO: Replace with real check
                    // bool isEquipped = currentOutfitData != null && slotIndex == 0;
                    bool isEquipped = false;

                    // TODO: Replace with real thumbnail loading
                    Sprite mockThumbnail = null;

                    view.ShowFullState(mockThumbnail, isEquipped, isHovered);
                    break;
            }
        }

        private async void HandleSaveClicked()
        {
            if (currentState != OutfitSlotState.Empty) return;

            cts = cts.SafeRestart();
            SetState(OutfitSlotState.Save);
            try
            {
                // TODO: Get current outfit data from a service
                var savedOutfit = await outfitsAPI.SaveOutfitAsync(slotIndex, new OutfitData(), cts.Token);
                SetState(OutfitSlotState.Full, savedOutfit);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                SetState(OutfitSlotState.Empty);
            }
        }

        private void HandleEquipClicked()
        {
            ReportHub.Log(ReportCategory.OUTFITS, "Equip outfit clicked");
        }

        private void HandleUnequipClicked()
        {
            ReportHub.Log(ReportCategory.OUTFITS, "Unequip outfit clicked");
        }

        private async void HandleDeleteClicked()
        {
            if (currentState != OutfitSlotState.Full) return;

            cts = cts.SafeRestart();

            var result = await ViewDependencies.ConfirmationDialogOpener.OpenConfirmationDialogAsync(
                new ConfirmationDialogParameter(OUTFIT_POPUP_DELETE_TEXT,
                    OUTFIT_POPUP_DELETE_CANCEL_TEXT,
                    OUTFIT_POPUP_DELETE_CONFIRM_TEXT, popupDeleteIcon,
                    false,
                    false),
                cts.Token).SuppressToResultAsync(ReportCategory.TRANSLATE);

            if (cts.IsCancellationRequested ||
                result.Value == ConfirmationResult.CANCEL ||
                !result.Success) return;

            SetState(OutfitSlotState.Save);

            try
            {
                await outfitsAPI.DeleteOutfitAsync(slotIndex, cts.Token);
                SetState(OutfitSlotState.Empty);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                SetState(OutfitSlotState.Full, currentOutfitData);
            }
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
        }
    }
}