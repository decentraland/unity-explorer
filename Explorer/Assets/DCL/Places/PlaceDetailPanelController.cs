using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.UI;
using DCL.WebRequests;
using MVC;
using System.Threading;
using Utility;

namespace DCL.Places
{
    public class PlaceDetailPanelController : ControllerBase<PlaceDetailPanelView, PlaceDetailPanelParameter>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private CancellationTokenSource panelCts = new ();
        private readonly ThumbnailLoader thumbnailLoader;

        public PlaceDetailPanelController(
            ViewFactoryMethod viewFactory,
            IWebRequestController webRequestController,
            ThumbnailLoader thumbnailLoader) : base(viewFactory)
        {
            this.thumbnailLoader = thumbnailLoader;
        }

        public override void Dispose()
        {
            panelCts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetCloseTasks());

        protected override void OnViewInstantiated()
        {

        }

        protected override void OnBeforeViewShow()
        {
            panelCts = panelCts.SafeRestart();
            viewInstance!.ConfigurePlaceData(inputData.PlaceData, thumbnailLoader, panelCts.Token);
        }

        protected override void OnViewClose()
        {
            panelCts.SafeCancelAndDispose();
        }
    }
}
