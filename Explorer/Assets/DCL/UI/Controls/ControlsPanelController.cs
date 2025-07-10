using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using UnityEngine.InputSystem;

namespace DCL.UI.Controls
{
    public class ControlsPanelController : ControllerBase<ControlsPanelView>
    {
        private readonly IMVCManager mvcManager;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        private bool closePanel;

        public ControlsPanelController(ViewFactoryMethod viewFactory, IMVCManager mvcManager) : base(viewFactory)
        {
            this.mvcManager = mvcManager;

            DCLInput.Instance.Shortcuts.Controls.performed += OnShortcutPressed;
        }

        private void OnShortcutPressed(InputAction.CallbackContext ctx)
        {
            if (State == ControllerState.ViewFocused)
                closePanel = true;
            else
                mvcManager.ShowAsync(IssueCommand()).Forget();
        }

        protected override void OnViewClose()
        {
            closePanel = false;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance!.closeButton.OnClickAsync(ct),
                UniTask.WaitUntil(() => closePanel, cancellationToken: ct)
            );

        public override void Dispose()
        {
            DCLInput.Instance.Shortcuts.Controls.performed -= OnShortcutPressed;
        }
    }
}
