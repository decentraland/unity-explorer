using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Views;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using MVC;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingController : ControllerBase<GiftingView, GiftingParams>
    {
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IProfileRepository profileRepository;
        
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private GiftingHeaderPresenter headerPresenter;
        private GiftingErrorsController? giftingErrorsController;
        private CancellationTokenSource? lifeCts;

        public GiftingController(ViewFactoryMethod viewFactory,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IProfileRepository profileRepository) : base(viewFactory)
        {
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.profileRepository = profileRepository;
        }

        public override void Dispose()
        {
            headerPresenter.Dispose();
        }
        
        private void OnPublishError()
        {
            giftingErrorsController!.Show();
        }
        
        #region MVC

        protected override void OnViewInstantiated()
        {
            if (viewInstance != null)
            {
                giftingErrorsController = new GiftingErrorsController(viewInstance!.ErrorNotification);
                headerPresenter = new GiftingHeaderPresenter(viewInstance.HeaderView,
                    profileRepository,
                    profileRepositoryWrapper);
            }
        }

        protected override void OnViewShow()
        {
            lifeCts = new CancellationTokenSource();
            
            viewInstance!.ErrorNotification.Hide(true);
            headerPresenter.SetupAsync(inputData.userId, lifeCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            giftingErrorsController!.Hide(true);
            lifeCts?.Cancel();
            lifeCts?.Dispose();
            lifeCts = null;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            return UniTask.WhenAny(
                viewInstance!.CloseButton.OnClickAsync(ct),
                viewInstance!.BackgroundButton.OnClickAsync(ct),
                viewInstance.CancelButton.OnClickAsync(ct)
            );
        }

        #endregion
    }
}