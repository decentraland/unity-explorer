using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Services.GiftItemLoader;
using DCL.Backpack.Gifting.Styling;
using DCL.Diagnostics;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.SharedSpaceManager;
using DCL.WebRequests;
using MVC;
using UnityEngine;
using Utility;

namespace DCL.Backpack.Gifting.Notifications
{
    public class GiftReceivedPopupController : ControllerBase<GiftReceivedPopupView, GiftReceivedNotification>
    {
        public const string UnknownItemName = "Unknown Item";
        
        private readonly IProfileRepository profileRepository;
        private readonly WearableStylingCatalog? wearableCatalog;
        private readonly IWebRequestController webRequestController;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IGiftItemLoaderService giftItemLoaderService;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private ImageController? imageController;
        private CancellationTokenSource? lifeCts;

        public GiftReceivedPopupController(
            ViewFactoryMethod viewFactory,
            IProfileRepository profileRepository,
            IGiftItemLoaderService giftItemLoaderService,
            WearableStylingCatalog wearableCatalog,
            IWebRequestController webRequestController,
            ISharedSpaceManager sharedSpaceManager)
            : base(viewFactory)
        {
            this.profileRepository = profileRepository;
            this.giftItemLoaderService = giftItemLoaderService;
            this.wearableCatalog = wearableCatalog;
            this.webRequestController = webRequestController;
            
            this.sharedSpaceManager = sharedSpaceManager;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            if (viewInstance?.GiftItemView?.ThumbnailImageView != null)
            {
                imageController = new ImageController(viewInstance.GiftItemView.ThumbnailImageView, webRequestController);
                imageController.SpriteLoaded += OnImageLoaded;
            }
        }

        protected override void OnViewShow()
        {
            viewInstance!.SubTitleText.text = GiftingTextIds.GiftOpenedTitle;
            viewInstance.ItemNameText.text = GiftingTextIds.GiftLoading;
            viewInstance.GiftItemView.SetLoading();
            
            lifeCts = new CancellationTokenSource();

            LoadFullDataAsync(inputData, lifeCts.Token)
                .Forget();

            PlayShowAnimationAsync(lifeCts.Token)
                .Forget();
        }

        protected override void OnViewClose()
        {
            lifeCts.SafeCancelAndDispose();
            if (imageController != null)
            {
                imageController.SpriteLoaded -= OnImageLoaded;
                imageController.StopLoading();
            }
        }


        private async UniTask LoadFullDataAsync(GiftReceivedNotification notification, CancellationToken ct)
        {
            try
            {
                var (profile, itemData) = await UniTask.WhenAll(
                    profileRepository.GetAsync(notification.Metadata.SenderAddress, ct),
                    giftItemLoaderService.LoadItemMetadataAsync(notification.Metadata.TokenUri, ct)
                );

                if (ct.IsCancellationRequested || viewInstance == null)
                    return;

                if (profile != null)
                {
                    string hexColor = ColorUtility.ToHtmlStringRGB(profile.UserNameColor);
                    viewInstance!.TitleText.text = string.Format(GiftingTextIds.GiftReceivedFromFormat, hexColor, profile.Name);
                }

                if (itemData.HasValue)
                {
                    var data = itemData.Value;
                    viewInstance!.ItemNameText.text = data.Name;

                    if (wearableCatalog != null)
                    {
                        viewInstance.GiftItemView.ConfigureAttributes(
                            rarityBg: wearableCatalog.GetRarityBackground(data.Rarity),
                            flapColor: wearableCatalog.GetRarityFlapColor(data.Rarity),
                            categoryIcon: wearableCatalog.GetCategoryIcon(data.Category)
                        );
                    }

                    if (!string.IsNullOrEmpty(data.ImageUrl) && imageController != null)
                    {
                        imageController.RequestImage(data.ImageUrl, fitAndCenterImage: true);
                    }
                    else
                    {
                        viewInstance.GiftItemView.SetLoadedState();
                    }
                }
                else
                {
                    viewInstance!.ItemNameText.text = UnknownItemName;
                    viewInstance.GiftItemView.SetLoadedState();
                }
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.GIFTING);
                if (viewInstance == null) return;

                viewInstance.ItemNameText.text = UnknownItemName;
                viewInstance.GiftItemView.SetLoadedState();
            }
            
        }

        private void OnImageLoaded(Sprite sprite)
        {
            viewInstance?.GiftItemView.SetLoadedState();
        }

        private async UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            if (viewInstance !=  null)
            {
                await viewInstance.BackgroundRaysAnimation.ShowAnimationAsync(ct);

                if (viewInstance.Sound != null)
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.Sound);
            }
        }

        private async UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            if (viewInstance != null)
                await viewInstance.BackgroundRaysAnimation.HideAnimationAsync(ct);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (viewInstance == null) return;

            var closeBtn = viewInstance.CloseButton.OnClickAsync(ct);
            var backpackBtn = viewInstance.OpenBackpackButton.OnClickAsync(ct);
            var bgBtn = viewInstance.BackgroundOverlayButton != null
                ? viewInstance.BackgroundOverlayButton.OnClickAsync(ct)
                : UniTask.Never(ct);

            int result = await
                UniTask.WhenAny(closeBtn, backpackBtn, bgBtn);

            if (result == 1)
                await sharedSpaceManager.OpenBackpackAsync();

            await PlayHideAnimationAsync(CancellationToken.None);
            lifeCts.SafeCancelAndDispose();
        }
    }
}