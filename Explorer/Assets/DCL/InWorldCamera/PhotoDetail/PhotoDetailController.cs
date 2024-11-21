using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class PhotoDetailController : ControllerBase<PhotoDetailView, PhotoDetailParameter>
    {
        private bool isClosing;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public PhotoDetailController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.closeButton.onClick.AddListener(CloseButtonClicked);
        }

        protected override void OnBeforeViewShow() =>
            isClosing = false;

        private void CloseButtonClicked() =>
            isClosing = true;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WaitWhile(() => !isClosing, cancellationToken: ct);

    }

    public readonly struct PhotoDetailParameter
    {
        public readonly string ReelId;

        public PhotoDetailParameter(string reelId)
        {
            this.ReelId = reelId;
        }
    }
}
