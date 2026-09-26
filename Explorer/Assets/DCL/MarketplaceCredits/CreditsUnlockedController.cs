using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.MarketplaceCredits
{
    public partial class CreditsUnlockedController : ControllerBase<CreditsUnlockedView, CreditsUnlockedController.Params>
    {
        private const int CREDITS_UNLOCKED_DURATION_MS = 5000;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public CreditsUnlockedController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        protected override void OnBeforeViewShow() =>
            viewInstance.SetCreditsText(MarketplaceCreditsUtils.FormatClaimedGoalReward(inputData.ClaimedCredits));

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance!.CloseButton.OnClickAsync(ct),
                UniTask.Delay(CREDITS_UNLOCKED_DURATION_MS, cancellationToken: ct));
    }
}
