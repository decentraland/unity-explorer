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
        private readonly CheckOutfitEquippedStateCommand equippedStateCommand;
        private readonly PrewarmWearablesCacheCommand prewarmWearablesCacheCommand;
        
        private readonly List<OutfitSlotPresenter> slotPresenters = new ();
        private CancellationTokenSource cts = new ();

        public OutfitsPresenter(OutfitsView view,
            IOutfitApplier outfitApplier,
            OutfitsCollection outfitsCollection,
            IWebBrowser webBrowser,
            IEquippedWearables equippedWearables,
            LoadOutfitsCommand loadOutfitsCommand,
            SaveOutfitCommand saveOutfitCommand,
            DeleteOutfitCommand deleteOutfitCommand,
            CheckOutfitsBannerVisibilityCommand bannerVisibilityCommand,
            CheckOutfitEquippedStateCommand equippedStateCommand,
            PrewarmWearablesCacheCommand prewarmWearablesCacheCommand,
            IAvatarScreenshotService screenshotService,
            CharacterPreviewControllerBase characterPreviewController,
            OutfitSlotPresenterFactory slotFactory)
        {
            this.view = view;
            this.outfitApplier = outfitApplier;
            this.outfitsCollection = outfitsCollection;
            this.equippedWearables = equippedWearables;
            this.webBrowser = webBrowser;
            this.loadOutfitsCommand = loadOutfitsCommand;
            this.saveOutfitCommand = saveOutfitCommand;
            this.deleteOutfitCommand = deleteOutfitCommand;
            this.bannerVisibilityCommand = bannerVisibilityCommand;
            this.equippedStateCommand = equippedStateCommand;
            this.prewarmWearablesCacheCommand = prewarmWearablesCacheCommand;
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
                slotPresenter.OnDeleteRequested += OnDeleteOutfitRequested;
                slotPresenter.OnEquipRequested += OnEquipOutfitRequested;

                slotPresenters.Add(slotPresenter);
            }
        }

        public async void Activate()
        {
            view.Activate();
            
            cts = cts.SafeRestart();
            await RefreshOutfitsAsync(cts.Token);
            await CheckBannerVisibilityAsync(cts.Token);
        }

        public void Deactivate()
        {
            cts.SafeCancelAndDispose();

            view.Deactivate();
        }

        private async UniTask RefreshOutfitsAsync(CancellationToken ct)
        {
            try
            {
                foreach (var p in slotPresenters) p.SetLoading();
                var outfits = await loadOutfitsCommand.ExecuteAsync(ct);
                if (ct.IsCancellationRequested) return;

                outfitsCollection.Update(outfits);

                var uniqueUrnStrings = outfits
                    .Where(o => o.outfit?.wearables != null)
                    .SelectMany(o => o.outfit.wearables)
                    .Distinct()
                    .ToList();

                var uniqueUrns = new HashSet<URN>(uniqueUrnStrings.Select(s => new URN(s)));
                prewarmWearablesCacheCommand.ExecuteAsync(uniqueUrns, ct).Forget();

                PopulateAllSlots(outfitsCollection.Get());
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
                    outfitsCollection.Get(),
                    ct);

                if (ct.IsCancellationRequested) return;

                if (savedItem == null)
                {
                    presenter.SetEmpty();
                    return;
                }

                outfitsCollection.AddOrReplace(savedItem);

                await TakeScreenshotAndDisplay(slotIndex);

                if (cts.Token.IsCancellationRequested) return;

                presenter.SetData(savedItem, loadThumbnail: false);

                OnEquipOutfitRequested(savedItem);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                presenter.SetEmpty();
            }
        }

        private async UniTask TakeScreenshotAndDisplay(int slotIndex)
        {
            var screenshot = await screenshotService
                .TakeAndSaveScreenshotAsync(characterPreviewController, slotIndex, cts.Token);

            var presenter = slotPresenters.FirstOrDefault(p => p.slotIndex == slotIndex);
            presenter?.SetThumbnail(screenshot);
        }

        private async void OnDeleteOutfitRequested(int slotIndex)
        {
            var presenter = slotPresenters.FirstOrDefault(p => p.slotIndex == slotIndex);
            if (presenter == null) return;

            var originalOutfitData = presenter.GetOutfitData();
            presenter.SetSaving();

            try
            {
                var outcome = await deleteOutfitCommand.ExecuteAsync(slotIndex, outfitsCollection.Get(), cts.Token);

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
            outfitApplier.Apply(outfitItem.outfit);
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
                presenter.OnDeleteRequested -= OnDeleteOutfitRequested;
                presenter.OnEquipRequested -= OnEquipOutfitRequested;
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