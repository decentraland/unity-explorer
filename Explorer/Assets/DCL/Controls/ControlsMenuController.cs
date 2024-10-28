using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using UnityEngine.InputSystem;

namespace DCL.Controls
{
    public class ControlsMenuController : ControllerBase<ControlsMenuView>
    {
        private readonly DCLInput dclInput;
        private readonly IMVCManager mvcManager;
        private UniTaskCompletionSource? closeViewTask;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        public ControlsMenuController(ViewFactoryMethod viewFactory,
            DCLInput dclInput,
            IMVCManager mvcManager)
            : base(viewFactory)
        {
            this.dclInput = dclInput;
            this.mvcManager = mvcManager;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.closeButton.onClick.AddListener(CloseMenu);
        }

        private void OpenMenu(InputAction.CallbackContext callbackContext) =>
            mvcManager.ShowAsync(IssueCommand()).Forget();

        private void CloseMenuCallback(InputAction.CallbackContext callbackContext) =>
            CloseMenu();

        private void CloseMenu() =>
            closeViewTask?.TrySetResult();

        protected override void OnViewShow()
        {
            dclInput.Shortcuts.ControlsMenu.performed -= OpenMenu;
            dclInput.Shortcuts.ControlsMenu.performed += CloseMenuCallback;
        }

        protected override void OnViewClose()
        {
            dclInput.Shortcuts.ControlsMenu.performed += OpenMenu;
            dclInput.Shortcuts.ControlsMenu.performed -= CloseMenuCallback;
        }

        public override void Dispose()
        {
            dclInput.Shortcuts.ControlsMenu.performed -= OpenMenu;
            dclInput.Shortcuts.ControlsMenu.performed -= CloseMenuCallback;
            viewInstance.closeButton.onClick.RemoveAllListeners();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeViewTask?.TrySetCanceled(ct);
            closeViewTask = new UniTaskCompletionSource();

            await closeViewTask.Task;
        }

        public readonly struct ControlsMenuParameter
        {

        }
    }
}
