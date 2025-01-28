using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using UnityEngine.InputSystem;

namespace DCL.Friends.UI.FriendPanel
{
    public class PersistentFriendPanelOpenerController : ControllerBase<PersistentFriendPanelOpenerView>
    {
        private readonly IMVCManager mvcManager;
        private readonly DCLInput dclInput;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public PersistentFriendPanelOpenerController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            DCLInput dclInput)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.dclInput = dclInput;

            mvcManager.OnViewShowed += OnViewShowed;
            mvcManager.OnViewClosed += OnViewClosed;
            RegisterHotkey();
        }

        public override void Dispose()
        {
            base.Dispose();

            mvcManager.OnViewShowed -= OnViewShowed;
            mvcManager.OnViewClosed -= OnViewClosed;
            viewInstance!.OpenFriendPanelButton.onClick.RemoveListener(OpenFriendsPanel);
            UnregisterHotkey();
        }

        private void RegisterHotkey()
        {
            dclInput.Shortcuts.FriendPanel.performed += OpenFriendsPanel;
        }

        private void UnregisterHotkey()
        {
            dclInput.Shortcuts.FriendPanel.performed -= OpenFriendsPanel;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.OpenFriendPanelButton.onClick.AddListener(OpenFriendsPanel);
        }

        private void OpenFriendsPanel(InputAction.CallbackContext obj) =>
            OpenFriendsPanel();

        private void OpenFriendsPanel() =>
            mvcManager.ShowAsync(FriendsPanelController.IssueCommand(new FriendsPanelParameter()));

        private void OnViewShowed(IController controller)
        {
            if (controller is not FriendsPanelController) return;

            viewInstance!.SetButtonStatePanelShow(true);
            UnregisterHotkey();
        }

        private void OnViewClosed(IController controller)
        {
            if (controller is not FriendsPanelController) return;

            viewInstance!.SetButtonStatePanelShow(false);
            RegisterHotkey();
        }
    }
}
