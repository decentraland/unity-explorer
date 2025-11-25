using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Styling;
using DCL.Diagnostics;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Profiles;
using DCL.UI.SharedSpaceManager;
using DCL.Web3;
using MVC;
using UnityEngine;
using Utility;

namespace DCL.Backpack.Gifting.Notifications
{
    public class GiftReceivedPopupController : ControllerBase<GiftReceivedPopupView, GiftReceivedNotificationMetadata>
    {
        private readonly IProfileRepository profileRepository;
        private readonly WearableStylingCatalog? wearableCatalog;
        private readonly IWearableStorage wearableStorage;
        private readonly IEmoteStorage emoteStorage;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly ISharedSpaceManager sharedSpaceManager;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private CancellationTokenSource? lifeCts;

        public GiftReceivedPopupController(
            ViewFactoryMethod viewFactory,
            IProfileRepository profileRepository,
            WearableStylingCatalog wearableCatalog,
            IWearableStorage wearableStorage,
            IEmoteStorage emoteStorage,
            IThumbnailProvider thumbnailProvider,
            ISharedSpaceManager sharedSpaceManager)
            : base(viewFactory)
        {
            this.profileRepository = profileRepository;
            this.wearableCatalog = wearableCatalog;
            this.wearableStorage = wearableStorage;
            this.emoteStorage = emoteStorage;
            this.thumbnailProvider = thumbnailProvider;
            this.sharedSpaceManager = sharedSpaceManager;
        }

        protected override void OnViewInstantiated()
        {
            // viewInstance!.CloseButton.onClick.AddListener(Close);
            // if (viewInstance.BackgroundOverlayButton != null)
            //     viewInstance.BackgroundOverlayButton.onClick.AddListener(Close);
            //
            // viewInstance.OpenBackpackButton.onClick.AddListener(OpenBackpackAndClose);
        }

        protected override void OnViewShow()
        {
            var metadata = inputData;

            viewInstance!.SubTitleText.text = "GIFT OPENED";
            viewInstance.ItemNameText.text = metadata.Item.GiftName;

            SetupItemVisualsAsync(metadata, CancellationToken.None).Forget();
            SetupSenderProfileAsync(metadata, CancellationToken.None).Forget();
            
            PlayAnimationAsync()
                .Forget();
        }

        protected override void OnViewClose()
        {
            // Fade out background and then close
            lifeCts.SafeCancelAndDispose();
        }

        private async UniTaskVoid SetupItemVisualsAsync(GiftReceivedNotificationMetadata metadata, CancellationToken ct)
        {
            var itemView = viewInstance!.GiftItemView;

            // 1. Set Loading State
            itemView.SetLoading();

            // 2. Configure Rarity/Category Backgrounds immediately (using Metadata strings)
            if (wearableCatalog != null)
            {
                string rarity = string.IsNullOrEmpty(metadata.Item.GiftRarity) ? "base" : metadata.Item.GiftRarity;

                itemView.ConfigureAttributes(
                    rarityBg: wearableCatalog.GetRarityBackground(rarity),
                    flapColor: wearableCatalog.GetRarityFlapColor(rarity),
                    categoryIcon: !string.IsNullOrEmpty(metadata.Item.GiftCategory)
                        ? wearableCatalog.GetCategoryIcon(metadata.Item.GiftCategory)
                        : null
                );
            }

            // 3. Fetch Thumbnail using DCL Providers
            // We treat TokenId as the URN here.
            string urn = metadata.Item.TokenId;
            Sprite? thumbnail = null;

            // try
            // {
            //     // A. Try finding as Wearable
            //     if (wearableStorage.TryGet(urn, out var wearable))
            //     {
            //         thumbnail = await thumbnailProvider.GetAsync(wearable, ct);
            //     }
            //     // B. Try finding as Emote
            //     else if (emoteStorage.TryGet(urn, out var emote))
            //     {
            //         thumbnail = await thumbnailProvider.GetAsync(emote, ct);
            //     }
            //     // C. Fallback: If local storage doesn't have it (unlikely if gifted), 
            //     // we could fallback to metadata.Item.ImageUrl here if we had a generic loader,
            //     // but usually Gifting implies the items are catalogued.
            // }
            // catch (System.Exception e)
            // {
            //     Debug.LogException(e);
            // }

            // 4. Set Result
            if (ct.IsCancellationRequested) return;

            if (thumbnail != null)
            {
                itemView.SetThumbnail(thumbnail);
            }
            else
            {
                ReportHub.LogWarning(ReportCategory.GIFTING, $"[GiftPopup] Could not load thumbnail for URN: {urn}");
            }
        }

        private async UniTaskVoid PlayAnimationAsync()
        {
            lifeCts = new CancellationTokenSource();
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

        private async UniTask SetupSenderProfileAsync(GiftReceivedNotificationMetadata metadata, CancellationToken ct)
        {
            var senderAddress = new Web3Address(metadata.Sender.Address);
            var profile = await profileRepository.GetAsync(senderAddress, ct);

            string name = profile != null ? profile.Name : metadata.Sender.Name;
            var nameColor = profile?.UserNameColor ?? Color.white;
            string hexColor = ColorUtility.ToHtmlStringRGB(nameColor);

            viewInstance!.TitleText.text = string.Format(
                GiftingTextIds.GiftReceivedFromFormat,
                hexColor,
                name
            );
        }

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
                await sharedSpaceManager.OpenBackpack();

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