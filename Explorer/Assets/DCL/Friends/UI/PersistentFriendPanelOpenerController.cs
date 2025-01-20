using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.Friends.UI
{
    public class PersistentFriendPanelOpenerController : ControllerBase<PersistentFriendPanelOpenerView>
    {
        private readonly IMVCManager mvcManager;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public PersistentFriendPanelOpenerController(ViewFactoryMethod viewFactory,
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
            viewInstance!.OpenFriendPanelButton.onClick.RemoveListener(OpenEmoteWheel);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.OpenFriendPanelButton.onClick.AddListener(OpenEmoteWheel);
        }

        private void OpenEmoteWheel()
        {
            mvcManager.ShowAsync(FriendsPanelController.IssueCommand(new FriendsPanelParameter(FriendsPanelController.FriendsPanelTab.FRIENDS))).Forget();
        }

        private void OnViewShowed(IController controller)
        {
            if (controller is not FriendsPanelController) return;

            viewInstance!.SetButtonStatePanelShow(true);
        }

        private void OnViewClosed(IController controller)
        {
            if (controller is not FriendsPanelController) return;

            viewInstance!.SetButtonStatePanelShow(false);
        }
    }
}
