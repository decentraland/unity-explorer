using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using MVC;
using System;
using System.Threading;

namespace DCL.RewardPanel
{
    public class RewardPanelController : ControllerBase<RewardPanelView>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public RewardPanelController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            viewInstance.ContinueButton.OnClickAsync(ct);
    }
}
