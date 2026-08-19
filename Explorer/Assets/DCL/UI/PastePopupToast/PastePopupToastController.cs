using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using MVC;
using System;
using System.Threading;

namespace DCL.UI
{
    public class PastePopupToastController : ControllerBase<PastePopupToastView, PastePopupToastData>
    {
        private readonly ClipboardManager clipboardManager;

        public PastePopupToastController(ViewFactoryMethod viewFactory, ClipboardManager clipboardManager) : base(viewFactory)
        {
            this.clipboardManager = clipboardManager;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            viewInstance!.PasteButton.onClick.AddListener(OnPasteButtonClicked);
        }

        protected override void OnBeforeViewShow()
        {
            viewInstance!.PasteToastPosition.position = inputData.Position;
            base.OnBeforeViewShow();
        }

        private void OnPasteButtonClicked()
        {
            clipboardManager.Paste(this);
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(inputData.CloseTask ?? UniTask.Never(ct),
                viewInstance!.PasteButton.OnClickAsync(ct));
    }
}
