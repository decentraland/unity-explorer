using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.EmotesWheel
{
    public class PersistentEmoteWheelOpenerController : ControllerBase<PersistentEmoteWheelOpenerView>
    {
        private readonly IMVCManager mvcManager;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public PersistentEmoteWheelOpenerController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;

            mvcManager.OnViewShowed += OnViewShowed;
            mvcManager.OnViewClosed += OnViewClosed;
        }

        public override void Dispose()
        {
            base.Dispose();

            mvcManager.OnViewShowed -= OnViewShowed;
            mvcManager.OnViewClosed -= OnViewClosed;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance.OpenEmoteWheelButton.onClick.AddListener(OpenEmoteWheel);
        }

        private void OpenEmoteWheel()
        {
            mvcManager.ShowAsync(EmotesWheelController.IssueCommand()).Forget();
        }

        private void OnViewShowed(IController controller)
        {
            if (controller is not EmotesWheelController) return;

            // TODO: this should not be handled here. Instead it should be handled by a Toggle component in the view
            viewInstance.EmotesDisabledContainer.SetActive(false);
            viewInstance.EmotesEnabledContainer.SetActive(true);
        }

        private void OnViewClosed(IController controller)
        {
            if (controller is not EmotesWheelController) return;

            // TODO: this should not be handled here. Instead it should be handled by a Toggle component in the view
            viewInstance.EmotesDisabledContainer.SetActive(true);
            viewInstance.EmotesEnabledContainer.SetActive(false);
        }
    }
}
