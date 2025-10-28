using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Views;
using MVC;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingController : ControllerBase<GiftingView, GiftingParams>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public GiftingController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }


        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            return UniTask.WhenAny(viewInstance!.CloseButton.OnClickAsync(ct));
        }
    }
}