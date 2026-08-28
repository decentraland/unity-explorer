using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.UI.UpgradeGuestAccountPopup
{
    public class UpgradeGuestAccountPopupController : ControllerBase<UpgradeGuestAccountPopupView>
    {
        private UniTaskCompletionSource lifeCycleTask = new ();

        public UpgradeGuestAccountPopupController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.UpgradeAccountButton.onClick.AddListener(UpgradeAccount);
            viewInstance.CloseButton.onClick.AddListener(Close);
        }

        private void Close()
        {
            lifeCycleTask.TrySetResult();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            lifeCycleTask = new UniTaskCompletionSource();
            return lifeCycleTask.Task.AttachExternalCancellation(ct);
        }

        private void UpgradeAccount()
        {
            // TODO: show link email flow
            lifeCycleTask.TrySetResult();
        }
    }
}
