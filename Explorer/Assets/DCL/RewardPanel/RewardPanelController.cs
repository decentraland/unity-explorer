using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.UI;
using DCL.WebRequests;
using JetBrains.Annotations;
using MVC;
using Nethereum.ABI.Model;
using System;
using System.Threading;

namespace DCL.RewardPanel
{
    public class RewardPanelController : ControllerBase<RewardPanelView, RewardPanelParameter>
    {
        private readonly IWebRequestController webRequestController;
        private readonly NFTColorsSO nftRarityColors;
        private readonly NftTypeIconSO nftRarityBackgrounds;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;
        private ImageController imageController;

        public RewardPanelController(
            ViewFactoryMethod viewFactory,
            IWebRequestController webRequestController,
            NFTColorsSO nftRarityColors,
            NftTypeIconSO nftRarityBackgrounds) : base(viewFactory)
        {
            this.webRequestController = webRequestController;
            this.nftRarityColors = nftRarityColors;
            this.nftRarityBackgrounds = nftRarityBackgrounds;
        }

        protected override void OnViewInstantiated()
        {
            imageController = new ImageController(viewInstance.ThumbnailImage, webRequestController);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            imageController.RequestImage(inputData.ImageUrl);
            viewInstance.ItemName.text = inputData.WearableName;
            viewInstance.RaysImage.color = nftRarityColors.GetColor(inputData.Category);
            viewInstance.RarityBackground.sprite = nftRarityBackgrounds.GetTypeImage(inputData.Category);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            viewInstance.ContinueButton.OnClickAsync(ct);

    }

    public readonly struct RewardPanelParameter
    {
        public readonly string ImageUrl;
        public readonly string WearableName;
        public readonly string Category;

        public RewardPanelParameter(string imageUrl, string wearableName, string category)
        {
            ImageUrl = imageUrl;
            WearableName = wearableName;
            Category = category;
        }
    }
}
