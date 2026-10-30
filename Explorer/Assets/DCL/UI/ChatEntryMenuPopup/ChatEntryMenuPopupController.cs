using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using MVC;
using System.Threading;

namespace DCL.UI
{
    public class ChatEntryMenuPopupController : ControllerBase<ChatEntryMenuPopupView, ChatEntryMenuPopupData>
    {
        private readonly ClipboardManager clipboardManager;

        public ChatEntryMenuPopupController(ViewFactoryMethod viewFactory, ClipboardManager clipboardManager) : base(viewFactory)
        {
            this.clipboardManager = clipboardManager;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            viewInstance!.CopyButton.onClick.AddListener(OnCopyButtonClicked);
        }

        protected override void OnBeforeViewShow()
        {
            viewInstance!.MenuPosition.position = inputData.Position;
            base.OnBeforeViewShow();
        }

        private void OnCopyButtonClicked()
        {
            clipboardManager.CopyAndSanitize(this, inputData.CopiedText);
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            await UniTask.WhenAny(inputData.CloseTask ?? UniTask.Never(ct),
                viewInstance!.CopyButton.OnClickAsync(ct));


        protected override void OnViewClose()
        {
            inputData.OnPopupClose?.Invoke();
            base.OnViewClose();
        }
    }
}
