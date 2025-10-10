using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits;
using DCL.Backpack.AvatarSection.Outfits.Banner;
using DCL.Backpack.AvatarSection.Outfits.Commands;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Backpack.AvatarSection.Outfits.Slots;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.Slots;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI;
using Runtime.Wearables;
using UnityEngine;
using Utility;

namespace DCL.Backpack
{
    public class OutfitsController : ISection, IDisposable
    {
        private readonly OutfitsView view;
        private readonly IOutfitsService outfitsService;
        private readonly IEquippedWearables equippedWearables;
        private readonly IWebBrowser webBrowser;
        private readonly BackpackCommandBus commandBus;
        private readonly OutfitBannerPresenter outfitBannerPresenter;
        private readonly DeleteOutfitCommand deleteOutfitCommand;
        private readonly IAvatarScreenshotService screenshotService;
        private readonly CharacterPreviewControllerBase characterPreviewController;
        private readonly List<OutfitSlotPresenter> slotPresenters = new ();
        private CancellationTokenSource cts = new ();

        private Profile? profile;

        public OutfitsController(OutfitsView view,
            IOutfitsService outfitsService,
            IWebBrowser webBrowser,
            BackpackCommandBus commandBus,
            IEquippedWearables equippedWearables,
            DeleteOutfitCommand deleteOutfitCommand,
            IAvatarScreenshotService screenshotService,
            CharacterPreviewControllerBase characterPreviewController)
        {
            this.view = view;
            this.outfitsService = outfitsService;
            this.equippedWearables = equippedWearables;
            this.webBrowser = webBrowser;
            this.commandBus = commandBus;
            this.deleteOutfitCommand = deleteOutfitCommand;
            this.screenshotService = screenshotService;
            this.characterPreviewController = characterPreviewController;

            outfitBannerPresenter = new OutfitBannerPresenter(view.OutfitsBanner,
                OnGetANameClicked, OnLinkClicked);

            for (int i = 0; i < view.BaseOutfitSlots.Length; i++)
                slotPresenters.Add(CreateSlotPresenter(view.BaseOutfitSlots[i], i));

            for (int i = 0; i < view.ExtraOutfitSlots.Length; i++)
            {
                int slotIndex = i + view.BaseOutfitSlots.Length;
                slotPresenters.Add(CreateSlotPresenter(view.ExtraOutfitSlots[i], slotIndex));
            }
        }

        private OutfitSlotPresenter CreateSlotPresenter(OutfitSlotView slotView, int slotIndex)
        {
            var presenter = new OutfitSlotPresenter(
                view.OutfitPopoupDeleteIcon,
                slotView,
                slotIndex,
                this,
                screenshotService
            );
            return presenter;
        }

        public async void Activate()
        {
            view.gameObject.SetActive(true);
            view.BackpackSearchBar.Activate(false);
            view.BackpackSortDropdown.Activate(false);

            cts = cts.SafeRestart();

            try
            {
                foreach (var presenter in slotPresenters)
                    presenter.SetLoading();

                await outfitsService.LoadOutfitsAsync(cts.Token);

                var currentOutfits = outfitsService.GetCurrentOutfits();
                PopulateAllSlots(currentOutfits);

                ReportHub.Log(ReportCategory.OUTFITS, currentOutfits.Count);

                CheckBannerVisibilityAsync(cts.Token).Forget();
            }
            catch (OperationCanceledException)
            {
                /* Suppress cancellation */
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                foreach (var presenter in slotPresenters)
                    presenter.SetEmpty();
            }
        }

        public void Deactivate()
        {
            cts.SafeCancelAndDispose();

            outfitsService.DeployOutfitsIfDirtyAsync(CancellationToken.None).Forget();

            view.gameObject.SetActive(false);
            view.BackpackSearchBar.Activate(true);
            view.BackpackSortDropdown.Activate(true);
        }

        public async void OnSaveOutfitRequested(int slotIndex)
        {
            var presenter = slotPresenters.FirstOrDefault(p => p.slotIndex == slotIndex);
            if (presenter == null) return;

            presenter.SetSaving();

            try
            {
                // 2. Call and AWAIT the new service method.
                var savedItem = await outfitsService.CreateAndSaveOutfitToServerAsync(slotIndex, equippedWearables, cts.Token);

                if (cts.Token.IsCancellationRequested) return;

                // 3. Update UI based on the outcome.
                if (savedItem != null)
                {
                    // Success! Update the slot with the confirmed saved data.
                    presenter.SetData(savedItem);
                    OnEquipOutfitRequested(savedItem); // Keep this if you want to auto-equip
                    TakeScreenshotAndDisplay(slotIndex).Forget(); // Take screenshot on success
                }
                else
                {
                    // Failure! Revert the slot to its empty state and notify the user.
                    presenter.SetEmpty();
                    // Optionally, show a toast/popup: "Failed to save outfit."
                }
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                presenter.SetEmpty(); // Revert on exception
            }
        }

        private async UniTaskVoid TakeScreenshotAndDisplay(int slotIndex)
        {
            var screenshot = await screenshotService
                .TakeAndSaveScreenshotAsync(characterPreviewController, slotIndex, cts.Token);

            var presenter = slotPresenters.FirstOrDefault(p => p.slotIndex == slotIndex);
            presenter?.SetThumbnail(screenshot);
        }

        public async UniTask OnDeleteOutfitRequested(int slotIndex)
        {
            var presenter = slotPresenters.FirstOrDefault(p => p.slotIndex == slotIndex);
            if (presenter == null) return;

            var originalOutfitData = presenter.GetOutfitData();
            presenter.SetSaving();

            // This command now represents a trustworthy, atomic operation.
            var outcome = await deleteOutfitCommand.ExecuteAsync(slotIndex, cts.Token);

            if (cts.Token.IsCancellationRequested) return;

            if (outcome == DeleteOutfitOutcome.Success)
            {
                // Success! The server AND local files are consistent. Update the UI.
                presenter.SetEmpty();
            }
            else
            {
                // Failure or Cancelled. The service has already reverted its state.
                // Now, we just revert the UI. This is correct.
                presenter.SetData(originalOutfitData);
            }
        }

        public void OnEquipOutfitRequested(OutfitItem outfitItem)
        {
            if (outfitItem?.outfit == null) return;

            // Use the BackpackCommandBus to equip all items from the outfit
            commandBus.SendCommand(new BackpackUnEquipAllCommand());
            commandBus.SendCommand(new BackpackEquipWearableCommand(outfitItem.outfit.bodyShape));

            foreach (string wearableId in outfitItem.outfit.wearables)
            {
                if (wearableId.Contains("croupier_shirt"))
                {
                    Debug.Log($"INVESTIGATION (Outfit): Equipping 'croupier_shirt'. URN sent to command bus: '{wearableId}'");
                }
                
                var wearableUrn = new URN(wearableId);
                commandBus.SendCommand(new BackpackEquipWearableCommand(wearableUrn.Shorten()));
            }

            commandBus.SendCommand(new BackpackChangeColorCommand(outfitItem.outfit.hair.color,
                WearableCategories.Categories.HAIR));

            commandBus.SendCommand(new BackpackChangeColorCommand(outfitItem.outfit.eyes.color,
                WearableCategories.Categories.EYES));

            commandBus.SendCommand(new BackpackChangeColorCommand(outfitItem.outfit.skin.color,
                WearableCategories.Categories.BODY_SHAPE));
        }

        private void PopulateAllSlots(IReadOnlyList<OutfitItem> currentOutfits)
        {
            var outfitsBySlot = currentOutfits.ToDictionary(o => o.slot);

            foreach (var presenter in slotPresenters)
            {
                if (outfitsBySlot.TryGetValue(presenter.slotIndex, out var outfitItem))
                    presenter.SetData(outfitItem);
                else
                    presenter.SetEmpty();
            }
        }

        private void OnLinkClicked(string url)
        {
            webBrowser.OpenUrl(url);
        }

        private void OnGetANameClicked()
        {
            webBrowser.OpenUrl("https://decentraland.org/marketplace/names/claim");
        }

        private async UniTask CheckBannerVisibilityAsync(CancellationToken ct)
        {
            bool showExtraOutfitSlots = await outfitsService.ShouldShowExtraOutfitSlotsAsync(ct);
            if (ct.IsCancellationRequested) return;

            view.ExtraSlotsContainer.SetActive(showExtraOutfitSlots);
            if (showExtraOutfitSlots) outfitBannerPresenter.Deactivate();
            else outfitBannerPresenter.Activate();
        }

        public void Dispose()
        {
            outfitBannerPresenter.Dispose();
            foreach (var presenter in slotPresenters)
                presenter.Dispose();
        }
        
        #region ISection

        public void Animate(int triggerId)
        {
            view.gameObject.SetActive(triggerId == UIAnimationHashes.IN);
        }

        public void ResetAnimator() { }

        public RectTransform GetRectTransform()
        {
            return (RectTransform)view.transform;
        }

        #endregion
    }
}