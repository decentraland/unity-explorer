using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.MarketplaceCredits
{
    public partial class CreditsUnlockedController : ControllerBase<CreditsUnlockedView, CreditsUnlockedController.Params>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public CreditsUnlockedController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        protected override void OnBeforeViewShow() =>
            viewInstance.CreditsText.text = MarketplaceCreditsUtils.FormatClaimedGoalReward(inputData.ClaimedCredits);

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance!.CloseButton.OnClickAsync(ct),
                UniTask.Delay(MarketplaceCreditsUtils.CREDITS_UNLOCKED_DURATION * 1000, cancellationToken: ct));
    }
}
