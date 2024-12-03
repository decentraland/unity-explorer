using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using System.Threading;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class EquippedWearableController : IDisposable
    {
        internal readonly EquippedWearableView view;
        private readonly IWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;

        private IWearable currentWearable;

		public EquippedWearableController(EquippedWearableView view,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            IThumbnailProvider thumbnailProvider,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons)
	    {
            this.view = view;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.thumbnailProvider = thumbnailProvider;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;

            this.view.wearableBuyButton.onClick.AddListener(BuyWearableButtonClicked);
        }

        public async UniTask LoadWearableAsync(IWearable wearable, CancellationToken ct)
        {
            currentWearable = wearable;

            view.wearableName.text = wearable.DTO.Metadata.name;
            view.rarityBackground.sprite = rarityBackgrounds.GetTypeImage(wearable.GetRarity());
            view.flapBackground.color = rarityColors.GetColor(wearable.GetRarity());
            view.categoryImage.sprite = categoryIcons.GetTypeImage(wearable.GetCategory());

            view.wearableIcon.sprite = await thumbnailProvider.GetAsync(wearable, ct);
        }

        private void BuyWearableButtonClicked()
        {
            async UniTaskVoid AnimateAndAwaitAsync()
            {
                await UniTask.Delay((int)(view.buyButtonAnimationDuration * 1000));
                webBrowser.OpenUrl(currentWearable.GetMarketplaceLink(decentralandUrlsSource));
            }

            AnimateAndAwaitAsync().Forget();
        }

        public void Release()
        {
            //TODO: remove if not needed
        }

        public void Dispose()
        {
            view.wearableBuyButton.onClick.RemoveAllListeners();
        }
    }
}
