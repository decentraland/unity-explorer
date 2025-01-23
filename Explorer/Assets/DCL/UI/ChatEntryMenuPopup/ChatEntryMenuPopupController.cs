using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using MVC;
using System.Threading;
using UnityEngine;

namespace DCL.UI
{
    public class ChatEntryMenuPopupController : ControllerBase<ChatEntryMenuPopupView, ChatEntryMenuPopupData>
    {
        private readonly IClipboardManager clipboardManager;

        public ChatEntryMenuPopupController(ViewFactoryMethod viewFactory, IClipboardManager clipboardManager) : base(viewFactory)
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
            clipboardManager.Copy(this, inputData.CopiedText);
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(inputData.CloseTask ?? UniTask.Never(ct),
                viewInstance!.CopyButton.OnClickAsync(ct));
    }

    public struct ChatEntryMenuPopupData
    {
        public readonly string CopiedText;
        public readonly UniTask? CloseTask;
        public readonly Vector2 Position;

        public ChatEntryMenuPopupData(Vector2 position, string copiedText, UniTask? closeTask = null)
        {
            Position = position;
            CopiedText = copiedText;
            CloseTask = closeTask;
        }
    }
}
