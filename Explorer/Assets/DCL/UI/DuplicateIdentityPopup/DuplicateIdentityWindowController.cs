using Cysharp.Threading.Tasks;
using DCL.Utility;
using MVC;
using System.Threading;

namespace DCL.UI.DuplicateIdentityPopup
{
    public class DuplicateIdentityWindowController : ControllerBase<DuplicateIdentityWindowView>
    {
        public DuplicateIdentityWindowController(
            ViewFactoryMethod viewFactory) : base(viewFactory) { }

        protected override void OnBeforeViewShow()
        {
            viewInstance!.ExitButton.onClick.AddListener(OnExitButtonClicked);
        }

        private void OnExitButtonClicked()
        {
            ExitUtils.Exit();
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) => UniTask.Never(ct);
    }
}


