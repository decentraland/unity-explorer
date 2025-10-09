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
        private readonly List<OutfitSlotPresenter> slotPresenters = new ();
        private CancellationTokenSource cts = new ();

        private Profile? profile;

        public OutfitsController(OutfitsView view,
            IOutfitsService outfitsService,
            IWebBrowser webBrowser,
            BackpackCommandBus commandBus,
            IEquippedWearables equippedWearables,
            DeleteOutfitCommand deleteOutfitCommand)
        {
            this.view = view;
            this.outfitsService = outfitsService;
            this.equippedWearables = equippedWearables;
            this.webBrowser = webBrowser;
            this.commandBus = commandBus;
            this.deleteOutfitCommand = deleteOutfitCommand;

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
                this
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

        public void OnSaveOutfitRequested(int slotIndex)
        {
            outfitsService.CreateAndUpdateLocalOutfit(slotIndex, equippedWearables, item =>
            {
                slotPresenters.FirstOrDefault(p => p.slotIndex == slotIndex)?.SetData(item);
                OnEquipOutfitRequested(item);
            });

            // var (hairColor, eyesColor, skinColor) = equippedWearables.GetColors();
            //
            // var liveWearableUrns = new List<string>();
            //
            // foreach (var equippedItem in equippedWearables.Items())
            // {
            //     if (equippedItem.Value != null)
            //     {
            //         liveWearableUrns.Add(equippedItem.Value.GetUrn());
            //     }
            // }
            //
            // if (!equippedWearables.Items().TryGetValue(WearableCategories.Categories.BODY_SHAPE,
            //         out var bodyShapeWearable) || bodyShapeWearable == null)
            // {
            //     ReportHub.LogError(ReportCategory.OUTFITS, "Cannot save outfit, Body Shape is not equipped!");
            //     return;
            // }
            //
            // string liveBodyShapeUrn = bodyShapeWearable.GetUrn();
            //
            // ReportHub.Log(ReportCategory.OUTFITS, $"INVESTIGATION (FINAL): BodyShape='{liveBodyShapeUrn}'," +
            //                                       $" Colors='{skinColor}, {eyesColor}, {hairColor}'," +
            //                                       $" Wearables='{liveWearableUrns.Count}'");
            //
            // var newItem = new OutfitItem
            // {
            //     slot = slotIndex, outfit = new Outfit
            //     {
            //         bodyShape = liveBodyShapeUrn, wearables = liveWearableUrns.ToArray(), eyes = new Eyes
            //         {
            //             color = eyesColor
            //         },
            //         hair = new Hair
            //         {
            //             color = hairColor
            //         },
            //         skin = new Skin
            //         {
            //             color = skinColor
            //         }
            //     }
            // };
            //
            // outfitsService.UpdateLocalOutfit(newItem);

            // slotPresenters.FirstOrDefault(p => p.slotIndex == slotIndex)?.SetData(newItem);
            //
            // OnEquipOutfitRequested(newItem);

            ReportHub.LogWarning(ReportCategory.OUTFITS, $"Save requested for outfit in slot {slotIndex} with UPDATED live data. ---");
        }

        public void OnDeleteOutfitRequested(int slotIndex)
        {
            // Tell the service to update its in-memory state
            deleteOutfitCommand.ExecuteAsync(slotIndex, CancellationToken.None, onConfirmed: () =>
            {
                outfitsService.DeleteLocalOutfit(slotIndex);
                slotPresenters.FirstOrDefault(p => p.slotIndex == slotIndex)?.SetEmpty();
            }).Forget();
        }

        public void OnEquipOutfitRequested(OutfitItem outfitItem)
        {
            if (outfitItem?.outfit == null) return;

            // Use the BackpackCommandBus to equip all items from the outfit
            commandBus.SendCommand(new BackpackUnEquipAllCommand());
            commandBus.SendCommand(new BackpackEquipWearableCommand(outfitItem.outfit.bodyShape));

            foreach (string wearableId in outfitItem.outfit.wearables)
            {
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