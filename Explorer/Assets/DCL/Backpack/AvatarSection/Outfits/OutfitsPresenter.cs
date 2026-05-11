using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.Backpack.AvatarSection.Outfits;
using DCL.Backpack.AvatarSection.Outfits.Banner;
using DCL.Backpack.AvatarSection.Outfits.Commands;
using DCL.Backpack.AvatarSection.Outfits.Events;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Backpack.AvatarSection.Outfits.Slots;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.CharacterPreview;
using DCL.Backpack.Slots;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.UI;
using UnityEngine;
using Utility;

namespace DCL.Backpack
{
    public class OutfitsPresenter : ISection, IDisposable
    {
        private readonly OutfitsView view;
        private readonly IEventBus eventBus;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IEquippedWearables equippedWearables;
        private readonly IWebBrowser webBrowser;
        private readonly OutfitApplier outfitApplier;
        private readonly OutfitBannerPresenter outfitBannerPresenter;
        private readonly OutfitsCollection outfitsCollection;
        private readonly IAvatarScreenshotService screenshotService;
        private readonly CharacterPreviewControllerBase characterPreviewController;
        private readonly OutfitSlotPresenterFactory slotFactory;

        // Commands
        private readonly LoadOutfitsCommand loadOutfitsCommand;
        private readonly SaveOutfitCommand saveOutfitCommand;
        private readonly DeleteOutfitCommand deleteOutfitCommand;
        private readonly CheckOutfitsBannerVisibilityCommand bannerVisibilityCommand;
        private readonly PreviewOutfitCommand previewOutfitCommand;

        private readonly List<OutfitSlotPresenter> slotPresenters = new ();
        private CancellationTokenSource cts = new ();
        private OutfitSlotPresenter? loadingSlot;

        public OutfitsPresenter(OutfitsView view,
            IEventBus eventBus,
            IBackpackEventBus backpackEventBus,
            OutfitApplier outfitApplier,
            OutfitsCollection outfitsCollection,
            IWebBrowser webBrowser,
            IEquippedWearables equippedWearables,
            LoadOutfitsCommand loadOutfitsCommand,
            SaveOutfitCommand saveOutfitCommand,
            DeleteOutfitCommand deleteOutfitCommand,
            CheckOutfitsBannerVisibilityCommand bannerVisibilityCommand,
            PreviewOutfitCommand previewOutfitCommand,
            IAvatarScreenshotService screenshotService,
            CharacterPreviewControllerBase characterPreviewController,
            OutfitSlotPresenterFactory slotFactory)
        {
            this.view = view;
            this.eventBus = eventBus;
            this.backpackEventBus = backpackEventBus;
            this.outfitApplier = outfitApplier;
            this.outfitsCollection = outfitsCollection;
            this.equippedWearables = equippedWearables;
            this.webBrowser = webBrowser;
            this.loadOutfitsCommand = loadOutfitsCommand;
            this.saveOutfitCommand = saveOutfitCommand;
            this.deleteOutfitCommand = deleteOutfitCommand;
            this.bannerVisibilityCommand = bannerVisibilityCommand;
            this.previewOutfitCommand = previewOutfitCommand;
            this.screenshotService = screenshotService;
            this.characterPreviewController = characterPreviewController;
            this.slotFactory = slotFactory;

            outfitBannerPresenter = new OutfitBannerPresenter(view.OutfitsBanner,
                OnGetANameClicked, OnLinkClicked);

            backpackEventBus.EquipOutfitCompletedEvent += EndSlotBusy;

            CreateOutfitSlots();
        }

        private void CreateOutfitSlots()
        {
            foreach (var slotView in view.BaseOutfitSlots.Concat(view.ExtraOutfitSlots))
            {
                int slotIndex = slotPresenters.Count;
                var slotPresenter = slotFactory.Create(slotView, slotIndex);

                slotPresenter.OnSaveRequested += OnSaveOutfitRequested;
                slotPresenter.OnDeleteRequested += OnDeleteOutfitRequested;
                slotPresenter.OnEquipRequested += OnEquipOutfitRequested;
                slotPresenter.OnPreviewRequested += OnPreviewOutfitRequested;

                slotPresenters.Add(slotPresenter);
            }
        }

        public void Activate()
        {
            view.Activate();

            cts = cts.SafeRestart();
            RefreshOutfitsAsync(cts.Token).Forget();
            CheckBannerVisibilityAsync(cts.Token).Forget();

            previewOutfitCommand.Restore();
        }

        public void Deactivate()
        {
            EndSlotBusy();
            characterPreviewController.StopEmotes();
            previewOutfitCommand.Restore();
            cts.SafeCancelAndDispose();
            view.Deactivate();
        }

        private async UniTask RefreshOutfitsAsync(CancellationToken ct)
        {
            try
            {
                SetAllSlotsToLoading();

                var outfits = await LoadAndCacheOutfitsAsync(ct);
                if (ct.IsCancellationRequested) return;

                // Update the UI with the primary data.
                PopulateAllSlots(outfits);
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

        private void SetAllSlotsToLoading()
        {
            foreach (var p in slotPresenters)
                p.SetLoading();
        }

        private async UniTask<IReadOnlyDictionary<int, OutfitItem>> LoadAndCacheOutfitsAsync(CancellationToken ct)
        {
            var outfits = await loadOutfitsCommand.ExecuteAsync(ct);
            if (ct.IsCancellationRequested) return new Dictionary<int, OutfitItem>();

            outfitsCollection.Update(outfits.Values);

            return outfits;
        }

        private void OnSaveOutfitRequested(int slotIndex)
        {
            OnSaveOutfitRequestedAsync(slotIndex, cts.Token).Forget();
        }

        private async UniTaskVoid OnSaveOutfitRequestedAsync(int slotIndex, CancellationToken ct)
        {
            if (!TryGetSlot(slotIndex, out var presenter)) return;

            presenter.SetSaving();

            try
            {
                var savedItem = await saveOutfitCommand.ExecuteAsync(slotIndex,
                    equippedWearables,
                    outfitsCollection.GetAll(),
                    CancellationToken.None);

                if (savedItem == null)
                {
                    presenter.SetEmpty();
                    return;
                }

                outfitsCollection.AddOrReplace(savedItem);

                await TakeScreenshotAndDisplayAsync(slotIndex, ct);

                if (cts.Token.IsCancellationRequested) return;

                presenter.SetData(savedItem, loadThumbnail: false);
                presenter.PlaySaveOutfitSound();

                UpdateFirstEmptySlotPrompt();
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.OUTFITS, "Save outfit operation was cancelled.");
                presenter.SetEmpty();
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                presenter.SetEmpty();
            }
        }

        private async UniTask TakeScreenshotAndDisplayAsync(int slotIndex, CancellationToken ct)
        {
            var thumbnail = await screenshotService
                .CaptureAndSavePngAsync(characterPreviewController, slotIndex, ct);

            if (ct.IsCancellationRequested || thumbnail == null) return;

            if (TryGetSlot(slotIndex, out var presenter))
                presenter.SetThumbnail(thumbnail);
        }

        private void OnDeleteOutfitRequested(int slotIndex)
        {
            OnDeleteOutfitRequestedAsync(slotIndex).Forget();
        }

        private async UniTaskVoid OnDeleteOutfitRequestedAsync(int slotIndex)
        {
            if (!TryGetSlot(slotIndex, out var presenter)) return;

            var originalOutfitData = presenter.GetOutfitData();
            if (originalOutfitData == null)
            {
                // This case should not happen
                // if the delete button is only on full slots
                ReportHub.LogWarning(ReportCategory.OUTFITS, "Attempted to delete an outfit from an empty slot.");
                return;
            }

            presenter.SetSaving();

            try
            {
                var outcome = await deleteOutfitCommand.ExecuteAsync(slotIndex,
                    outfitsCollection.GetAll(),
                    CancellationToken.None);

                if (cts.Token.IsCancellationRequested)
                {
                    presenter.SetData(originalOutfitData);
                    return;
                }

                if (outcome == DeleteOutfitOutcome.Success)
                {
                    outfitsCollection.Remove(slotIndex);

                    presenter.SetEmpty();
                    presenter.PlayDeleteOutfitSound();
                    UpdateFirstEmptySlotPrompt();
                }
                else
                {
                    presenter.SetData(originalOutfitData);
                }
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                presenter.SetData(originalOutfitData);
            }
        }

        private void OnEquipOutfitRequested(OutfitItem outfitItem)
        {
            if (outfitItem?.outfit == null) return;

            BeginSlotBusy(outfitItem.slot);

            GenerateThumbnailIfMissingAsync(outfitItem.slot, cts.Token).Forget();

            previewOutfitCommand.Commit();

            outfitApplier.Apply(outfitItem.outfit);

            eventBus.Publish(new OutfitsEvents.EquipOutfitEvent());

            PlayRandomEmote();
        }

        private void BeginSlotBusy(int slotIndex)
        {
            EndSlotBusy();

            if (!TryGetSlot(slotIndex, out var slot)) return;

            loadingSlot = slot;
            slot.SetEquipLoading(true);
            for (int i = 0; i < slotPresenters.Count; i++)
                slotPresenters[i].SetHoverEnabled(slotPresenters[i] == slot);
        }

        private void EndSlotBusy()
        {
            loadingSlot?.SetEquipLoading(false);
            for (int i = 0; i < slotPresenters.Count; i++)
                slotPresenters[i].SetHoverEnabled(true);
            loadingSlot = null;
        }

        private bool TryGetSlot(int slotIndex, out OutfitSlotPresenter slot)
        {
            slot = null!;

            foreach (var presenter in slotPresenters)
                if (presenter.slotIndex == slotIndex)
                {
                    slot = presenter;
                    return true;
                }

            return false;
        }

        private void OnPreviewOutfitRequested(OutfitItem outfitItem)
        {
            OnPreviewOutfitRequestedAsync(outfitItem).Forget();
        }

        private async UniTaskVoid OnPreviewOutfitRequestedAsync(OutfitItem outfitItem)
        {
            if (outfitItem?.outfit == null) return;

            BeginSlotBusy(outfitItem.slot);

            try
            {
                await previewOutfitCommand.ExecuteAsync(outfitItem, cts.Token);
                GenerateThumbnailIfMissingAsync(outfitItem.slot, cts.Token).Forget();
            }
            catch (OperationCanceledException) { EndSlotBusy(); }
            catch (Exception ex)
            {
                EndSlotBusy();
                ReportHub.LogException(ex, ReportCategory.OUTFITS);
            }
        }

        private async UniTaskVoid GenerateThumbnailIfMissingAsync(int slotIndex, CancellationToken ct)
        {
            if (!TryGetSlot(slotIndex, out var presenter) || presenter.HasThumbnail())
                return;

            try
            {
                await TakeScreenshotAndDisplayAsync(slotIndex, ct);
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.OUTFITS, $"On-demand thumbnail generation for slot {slotIndex} was cancelled.");
            }
        }

        private void PopulateAllSlots(IReadOnlyDictionary<int, OutfitItem> outfits)
        {
            foreach (var presenter in slotPresenters)
            {
                if (outfits.TryGetValue(presenter.slotIndex, out var outfitItem))
                {
                    presenter.SetData(outfitItem);
                    presenter.SetAsFirstEmptyAndReadyToSave(false);
                }
                else
                    presenter.SetEmpty();
            }

            UpdateFirstEmptySlotPrompt();
        }

        private void UpdateFirstEmptySlotPrompt()
        {
            bool allSlotsAreEmpty = true;

            foreach (var presenter in slotPresenters)
            {
                if (!presenter.IsEmpty())
                {
                    allSlotsAreEmpty = false;
                    break;
                }
            }

            for (int i = 0; i < slotPresenters.Count; i++)
            {
                var presenter = slotPresenters[i];
                if (allSlotsAreEmpty && i == 0)
                    presenter.SetAsFirstEmptyAndReadyToSave(true);
                else
                    presenter.SetAsFirstEmptyAndReadyToSave(false);
            }
        }

        private void OnLinkClicked(string url)
        {
            webBrowser.OpenUrl(url);
        }


        private void OnGetANameClicked()
        {
            webBrowser.OpenUrl("https://decentraland.org/marketplace/names/claim");
            eventBus.Publish(new OutfitsEvents.ClaimExtraOutfitsEvent());
        }

        private async UniTask CheckBannerVisibilityAsync(CancellationToken ct)
        {
            bool showExtraOutfitSlots = await bannerVisibilityCommand.ShouldShowExtraOutfitSlotsAsync(ct);
            if (ct.IsCancellationRequested) return;

            view.ExtraSlotsContainer.SetActive(showExtraOutfitSlots);
            if (showExtraOutfitSlots) outfitBannerPresenter.Deactivate();
            else outfitBannerPresenter.Activate();
        }

        private void ResetEmotes()
        {
            if (characterPreviewController is BackpackCharacterPreviewController backpackController)
                backpackController.ResetEmote();
        }

        private void PlayRandomEmote()
        {
            ResetEmotes();

            if (characterPreviewController is BackpackCharacterPreviewController backpackController)
                backpackController.PlayRandomEmote();
        }

        public void Dispose()
        {
            backpackEventBus.EquipOutfitCompletedEvent -= EndSlotBusy;
            outfitBannerPresenter.Dispose();
            foreach (var presenter in slotPresenters)
            {
                presenter.OnSaveRequested -= OnSaveOutfitRequested;
                presenter.OnDeleteRequested -= OnDeleteOutfitRequested;
                presenter.OnEquipRequested -= OnEquipOutfitRequested;
                presenter.OnPreviewRequested -= OnPreviewOutfitRequested;
                presenter.Dispose();
            }
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
