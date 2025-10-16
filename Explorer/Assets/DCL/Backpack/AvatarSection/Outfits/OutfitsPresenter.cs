using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.Backpack.AvatarSection.Outfits;
using DCL.Backpack.AvatarSection.Outfits.Banner;
using DCL.Backpack.AvatarSection.Outfits.Commands;
using DCL.Backpack.AvatarSection.Outfits.Events;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Backpack.AvatarSection.Outfits.Slots;
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
        private readonly IEquippedWearables equippedWearables;
        private readonly IWebBrowser webBrowser;
        private readonly IOutfitApplier outfitApplier;
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
        private readonly PrewarmWearablesCacheCommand prewarmWearablesCacheCommand;
        private readonly PreviewOutfitCommand previewOutfitCommand;
        
        private readonly List<OutfitSlotPresenter> slotPresenters = new ();
        private CancellationTokenSource cts = new ();

        public OutfitsPresenter(OutfitsView view,
            IEventBus eventBus,
            IOutfitApplier outfitApplier,
            OutfitsCollection outfitsCollection,
            IWebBrowser webBrowser,
            IEquippedWearables equippedWearables,
            LoadOutfitsCommand loadOutfitsCommand,
            SaveOutfitCommand saveOutfitCommand,
            DeleteOutfitCommand deleteOutfitCommand,
            CheckOutfitsBannerVisibilityCommand bannerVisibilityCommand,
            PrewarmWearablesCacheCommand prewarmWearablesCacheCommand,
            PreviewOutfitCommand previewOutfitCommand,
            IAvatarScreenshotService screenshotService,
            CharacterPreviewControllerBase characterPreviewController,
            OutfitSlotPresenterFactory slotFactory)
        {
            this.view = view;
            this.eventBus = eventBus;
            this.outfitApplier = outfitApplier;
            this.outfitsCollection = outfitsCollection;
            this.equippedWearables = equippedWearables;
            this.webBrowser = webBrowser;
            this.loadOutfitsCommand = loadOutfitsCommand;
            this.saveOutfitCommand = saveOutfitCommand;
            this.deleteOutfitCommand = deleteOutfitCommand;
            this.bannerVisibilityCommand = bannerVisibilityCommand;
            this.prewarmWearablesCacheCommand = prewarmWearablesCacheCommand;
            this.previewOutfitCommand = previewOutfitCommand;
            this.screenshotService = screenshotService;
            this.characterPreviewController = characterPreviewController;
            this.slotFactory = slotFactory;

            outfitBannerPresenter = new OutfitBannerPresenter(view.OutfitsBanner,
                OnGetANameClicked, OnLinkClicked);

            CreateOutfitSlots();
        }

        private void CreateOutfitSlots()
        {
            foreach (var slotView in view.BaseOutfitSlots.Concat(view.ExtraOutfitSlots))
            {
                int slotIndex = slotPresenters.Count;
                var slotPresenter = slotFactory.Create(slotView, slotIndex);

                slotPresenter.OnSaveRequested += OnSaveOutfitRequested;
                slotPresenter.OnDeleteRequested += OnDeleteOutfitRequestedAsync;
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

                // Precache wearables in the background.
                PrewarmWearablesCacheAsync(outfits, ct).Forget();

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

        private async UniTask<IReadOnlyList<OutfitItem>> LoadAndCacheOutfitsAsync(CancellationToken ct)
        {
            var outfits = await loadOutfitsCommand.ExecuteAsync(ct);
            if (ct.IsCancellationRequested) return Array.Empty<OutfitItem>();
            outfitsCollection.Update(outfits);
            return outfits;
        }

        private async UniTaskVoid PrewarmWearablesCacheAsync(IReadOnlyList<OutfitItem> outfits, CancellationToken ct)
        {
            var uniqueUrnsToPrewarm = new HashSet<URN>();
            foreach (var outfitItem in outfits)
            {
                if (outfitItem.outfit?.wearables == null) continue;
                foreach (string urnString in outfitItem.outfit.wearables)
                    uniqueUrnsToPrewarm.Add(new URN(urnString));
            }

            await prewarmWearablesCacheCommand.ExecuteAsync(uniqueUrnsToPrewarm, ct);
        }

        private void OnSaveOutfitRequested(int slotIndex)
        {
            OnSaveOutfitRequestedAsync(slotIndex, cts.Token).Forget();
        }

        private async UniTaskVoid OnSaveOutfitRequestedAsync(int slotIndex, CancellationToken ct)
        {
            var presenter = slotPresenters.FirstOrDefault(p => p.slotIndex == slotIndex);
            if (presenter == null) return;
            
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

                await TakeScreenshotAndDisplayAsync(slotIndex);

                if (cts.Token.IsCancellationRequested) return;

                presenter.SetData(savedItem, loadThumbnail: false);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                presenter.SetEmpty();
            }
        }

        private async UniTask TakeScreenshotAndDisplayAsync(int slotIndex)
        {
            var screenshot = await screenshotService
                .TakeAndSaveScreenshotAsync(characterPreviewController, slotIndex, CancellationToken.None);

            var presenter = slotPresenters.FirstOrDefault(p => p.slotIndex == slotIndex);
            presenter?.SetThumbnail(screenshot);
        }

        private async void OnDeleteOutfitRequestedAsync(int slotIndex)
        {
            var presenter = slotPresenters.Find(p => p.slotIndex == slotIndex);
            if (presenter == null) return;

            var originalOutfitData = presenter.GetOutfitData();
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

            GenerateThumbnailIfMissingAsync(outfitItem.slot);

            previewOutfitCommand.Commit();
            
            outfitApplier.Apply(outfitItem.outfit);
            
            eventBus.Publish(new OutfitsEvents.EquipOutfitEvent());
        }

        private void OnPreviewOutfitRequested(OutfitItem outfitItem)
        {
            if (outfitItem?.outfit == null) return;

            ReportHub.Log(ReportCategory.OUTFITS, $"Previewing outfit in slot {outfitItem.slot}");

            previewOutfitCommand.ExecuteAsync(outfitItem, cts.Token).Forget();

            GenerateThumbnailIfMissingAsync(outfitItem.slot);
        }

        private void GenerateThumbnailIfMissingAsync(int slotIndex)
        {
            var presenter = slotPresenters.Find(p => p.slotIndex == slotIndex);

            if (presenter != null && !presenter.HasThumbnail())
            {
                ReportHub.Log(ReportCategory.OUTFITS, $"Thumbnail for slot {slotIndex} is missing. Generating on demand.");
                TakeScreenshotAndDisplayAsync(slotIndex).Forget();
            }
        }

        private void PopulateAllSlots(IReadOnlyList<OutfitItem> outfits)
        {
            var outfitsBySlot = outfits.ToDictionary(o => o.slot);

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
        
        public void Dispose()
        {
            outfitBannerPresenter.Dispose();
            foreach (var presenter in slotPresenters)
            {
                presenter.OnSaveRequested -= OnSaveOutfitRequested;
                presenter.OnDeleteRequested -= OnDeleteOutfitRequestedAsync;
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