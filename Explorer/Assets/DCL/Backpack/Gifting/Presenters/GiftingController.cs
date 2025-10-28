using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Views;
using DCL.Passport;
using MVC;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingController : ControllerBase<GiftingView, GiftingParams>
    {
        private GiftingErrorsController? giftingErrorsController;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public GiftingController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        private void OnPublishError()
        {
            giftingErrorsController!.Show();
        }

        #region MVC

        protected override void OnViewInstantiated()
        {
            giftingErrorsController = new GiftingErrorsController(viewInstance!.ErrorNotification);
        }

        protected override void OnViewShow()
        {
            viewInstance!.ErrorNotification.Hide(true);
        }

        protected override void OnViewClose()
        {
            giftingErrorsController!.Hide(true);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            return UniTask.WhenAny(
                viewInstance!.CloseButton.OnClickAsync(ct),
                viewInstance!.BackgroundButton.OnClickAsync(ct)
            );
        }

        #endregion
    }
}