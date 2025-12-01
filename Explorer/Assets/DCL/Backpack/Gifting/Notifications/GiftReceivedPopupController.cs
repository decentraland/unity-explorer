using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Services.GiftItemLoader;
using DCL.Backpack.Gifting.Styling;
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
            
            PlayAnimationAsync()
                .Forget();
        }

        protected override void OnViewClose()
        {
            lifeCts.SafeCancelAndDispose();
            imageController?.StopLoading();
        }


        private async UniTaskVoid LoadFullDataAsync(GiftReceivedNotification notification, CancellationToken ct)
        {
            var (profile, itemData) = await UniTask.WhenAll(
                profileRepository.GetAsync(notification.Metadata.SenderAddress, ct),
                giftItemLoaderService.LoadItemMetadataAsync(notification.Metadata.TokenUri, ct)
            );

            if (ct.IsCancellationRequested) return;

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
                viewInstance!.ItemNameText.text = "Unknown Item";
                viewInstance.GiftItemView.SetLoadedState();
            }
        }

        private void OnImageLoaded(Sprite sprite)
        {
            viewInstance?.GiftItemView?.SetLoadedState();
        }
        
        private async UniTaskVoid PlayAnimationAsync()
        {
            await PlayShowAnimationAsync(lifeCts.Token);
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

        // private async UniTask SetupSenderProfileAsync(GiftReceivedNotificationMetadata metadata, CancellationToken ct)
        // {
        //     var senderAddress = new Web3Address(metadata.Sender.Address);
        //     var profile = await profileRepository.GetAsync(senderAddress, ct);
        //
        //     string name = profile != null ? profile.Name : metadata.Sender.Name;
        //     var nameColor = profile?.UserNameColor ?? Color.white;
        //     string hexColor = ColorUtility.ToHtmlStringRGB(nameColor);
        //
        //     viewInstance!.TitleText.text = string.Format(
        //         GiftingTextIds.GiftReceivedFromFormat,
        //         hexColor,
        //         name
        //     );
        // }

        private void OpenBackpackAndClose()
        {
            Close();
        }

        private void Close()
        {
            viewInstance!
                .HideAsync(CancellationToken.None).Forget();
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

    public class GiftPopupItemViewModel : IGiftableItemViewModel
    {
        public string Urn { get; set; }
        public string DisplayName { get; }
        public Sprite Thumbnail { get; set; }
        public ThumbnailState ThumbnailState { get; set; }
        public int NftCount { get; set; }
        public string RarityId { get; set; }
        public string CategoryId { get; set; }
        public bool IsEquipped { get; set; }
        public IGiftable Giftable { get; }
        public bool IsGiftable { get; set; }
    }
}