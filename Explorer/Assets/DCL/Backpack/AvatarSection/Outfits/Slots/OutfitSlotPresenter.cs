using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.AvatarSection.Outfits.Repository;
using DCL.Backpack.AvatarSection.Outfits.Slots;
using DCL.Diagnostics;
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

        public override string ToString()
        {
            return $"BodyShape: {BodyShapeUrn}, Wearables: [{string.Join(", ", WearableUrns)}], " +
                   $"SkinColor: {SkinColor}, HairColor: {HairColor}, EyesColor: {EyesColor}, ThumbnailUrl: {ThumbnailUrl}";
        }
    }

    public enum OutfitSlotState { Empty, Loading, Full, Save }

    public class OutfitSlotPresenter : IDisposable
    {
        public readonly OutfitSlotView view;
        public readonly int slotIndex;
        private readonly OutfitsController ownerController;

        private CancellationTokenSource cts = new ();
        private readonly HoverHandler hoverHandler;
        private bool isHovered;
        private OutfitSlotState currentState;
        private OutfitItem? currentOutfitData;
        private readonly Sprite popupDeleteIcon;

        public OutfitSlotPresenter(
            Sprite popupDeleteIcon,
            OutfitSlotView view,
            int slotIndex,
            OutfitsController ownerController)
        {
            this.popupDeleteIcon = popupDeleteIcon;
            this.view = view;
            this.slotIndex = slotIndex;
            this.ownerController = ownerController;

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
        }

        public void SetEmpty()
        {
            SetState(OutfitSlotState.Empty);
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

                    bool isEquipped = false;
                    Sprite mockThumbnail = null;
                    view.ShowFullState(mockThumbnail, isEquipped, isHovered);
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

        // private async UniTask HandleSaveClickedAsync()
        // {
        //     if (currentState != OutfitSlotState.Empty) return;
        //
        //     cts = cts.SafeRestart();
        //
        //     var outfitToSave = new OutfitData();
        //
        //     var saveCmd = new SaveOutfitCommand(outfitService);
        //
        //     var outcome = await saveCmd.ExecuteAsync(
        //         slotIndex,
        //         outfitToSave,
        //         cts.Token,
        //         onConfirmed: () => SetState(OutfitSlotState.Save));
        //
        //     if (cts.IsCancellationRequested) return;
        //
        //     var (state, data) = outcome switch
        //     {
        //         SaveOutfitOutcome.Success => (OutfitSlotState.Full, (OutfitData?)outfitToSave),
        //         SaveOutfitOutcome.Cancelled => (OutfitSlotState.Empty, null),
        //         SaveOutfitOutcome.Failed => (OutfitSlotState.Empty, (OutfitData?)null)
        //     };
        //
        //     SetState(state, data);
        // }
        //
        // private async UniTask HandleDeleteClickedAsync()
        // {
        //     if (currentState != OutfitSlotState.Full) return;
        //
        //     cts = cts.SafeRestart();
        //
        //     var outfitDeleteCommand = new DeleteOutfitCommand(outfitService);
        //     var outcome = await outfitDeleteCommand.ExecuteAsync(slotIndex, cts.Token,
        //         onConfirmed: () =>
        //         {
        //             SetState(OutfitSlotState.Save);
        //         });
        //
        //     if (cts.IsCancellationRequested) return;
        //
        //     var (state, data) = outcome switch
        //     {
        //         DeleteOutfitOutcome.Success => (OutfitSlotState.Empty, (OutfitData?)null),
        //         DeleteOutfitOutcome.Cancelled => (OutfitSlotState.Full, null),
        //         DeleteOutfitOutcome.Failed => (OutfitSlotState.Full, currentOutfitData)
        //     };
        //
        //     SetState(state, data);
        // }

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