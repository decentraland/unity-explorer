using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.Backpack.AvatarSection.Outfits;
using DCL.Backpack.AvatarSection.Outfits.Banner;
using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.Slots;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.Web3.Identities;
using UnityEngine;
using Utility;

namespace DCL.Backpack
{
    public class OutfitsController : ISection, IDisposable
    {
        private readonly OutfitsView view;
        private readonly ISelfProfile selfProfile;
        private readonly IOutfitsService outfitsService;
        private readonly IWebBrowser webBrowser;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly OutfitBannerPresenter outfitBannerPresenter;
        private readonly List<OutfitSlotPresenter> slotPresenters = new ();
        private CancellationTokenSource cts = new ();

        private Profile? profile;

        public OutfitsController(OutfitsView view,
            ISelfProfile selfProfile,
            IOutfitsService outfitsService,
            IWebBrowser webBrowser,
            IWeb3IdentityCache web3IdentityCache,
            BackpackCommandBus commandBus)
        {
            this.view = view;
            this.selfProfile = selfProfile;
            this.outfitsService = outfitsService;
            this.webBrowser = webBrowser;
            this.web3IdentityCache = web3IdentityCache;

            outfitBannerPresenter = new OutfitBannerPresenter(view.OutfitsBanner,
                OnGetANameClicked, OnLinkClicked);

            for (int index = 0; index < view.BaseOutfitSlots.Length; index++)
            {
                slotPresenters.Add(new OutfitSlotPresenter(view.BaseOutfitSlots[index], index,
                    outfitsService,
                    commandBus));
            }

            for (int index = 0; index < view.ExtraOutfitSlots.Length; index++)
            {
                int slotIndex = index + view.BaseOutfitSlots.Length;
                slotPresenters.Add(new OutfitSlotPresenter(view.ExtraOutfitSlots[index], slotIndex,
                    outfitsService,
                    commandBus));
            }
        }

        /// <summary>
        ///     When activating outfits
        ///     - enable view
        ///     - reset slots
        ///     - initialize slots
        /// </summary>
        public async void Activate()
        {
            view.gameObject.SetActive(true);
            view.BackpackSearchBar.Activate(false);
            view.BackpackSortDropdown.Activate(false);

            cts = cts.SafeRestart();

            foreach (var presenter in slotPresenters)
                presenter.view.ShowLoadingState();

            try
            {
                await UniTask.WhenAll(
                    CheckBannerVisibilityAsync(cts.Token),
                    LoadAndDisplayOutfitsAsync(cts.Token)
                );
            }
            catch (OperationCanceledException)
            {
                /* Suppress cancellation */
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                slotPresenters.ForEach(p => p.SetEmpty());
            }
        }

        /// <summary>
        ///     When deactivating outfits
        ///     - reset slots
        ///     - deactivate view
        /// </summary>
        public void Deactivate()
        {
            cts.SafeCancelAndDispose();
            view.gameObject.SetActive(false);
            view.BackpackSearchBar.Activate(true);
            view.BackpackSortDropdown.Activate(true);
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
            if (web3IdentityCache.Identity == null) return;
            profile ??= await selfProfile.ProfileAsync(ct);

            if (profile != null)
            {
                if (profile.HasClaimedName)
                {
                    outfitBannerPresenter.Deactivate();
                    view.ExtraSlotsContainer.SetActive(true);
                }
                else
                {
                    outfitBannerPresenter.Activate();
                    view.ExtraSlotsContainer.SetActive(false);
                }
            }
        }

        private async UniTask LoadAndDisplayOutfitsAsync(CancellationToken ct)
        {
            var savedOutfits = await outfitsService.GetOutfitsAsync(ct);
            foreach (var presenter in slotPresenters)
            {
                if (savedOutfits.TryGetValue(presenter.slotIndex, out var outfitData))
                {
                    presenter.SetData(outfitData);
                }
                else
                    presenter.SetEmpty();
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

        public void Dispose()
        {
            outfitBannerPresenter.Dispose();
            foreach (var presenter in slotPresenters)
                presenter.Dispose();
        }
    }
}