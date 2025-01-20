using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using UnityEngine.InputSystem;

namespace DCL.UI.Controls
{
    public class ControlsPanelController : ControllerBase<ControlsPanelView>
    {
        private readonly IMVCManager mvcManager;
        private readonly DCLInput input;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        private bool closePanel;

        public ControlsPanelController(ViewFactoryMethod viewFactory, IMVCManager mvcManager, DCLInput input) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.input = input;

            input.Shortcuts.Controls.performed += OnShortcutPressed;
        }

        private void OnShortcutPressed(InputAction.CallbackContext ctx)
        {
            if (State == ControllerState.ViewFocused)
                closePanel = true;
            else
                mvcManager.ShowAsync(IssueCommand()).Forget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance!.closeButton.OnClickAsync(ct),
                UniTask.WaitUntil(() => input.UI.Close.WasPerformedThisFrame() || closePanel, cancellationToken: ct)
            );

        public override void Dispose()
        {
            input.Shortcuts.Controls.performed -= OnShortcutPressed;
        }
    }
}
