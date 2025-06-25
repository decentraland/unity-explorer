using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using MVC;
using System;
using System.Threading;
using UnityEngine;

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

    public struct ChatEntryMenuPopupData
    {
        public readonly string CopiedText;
        public readonly UniTask? CloseTask;
        public readonly Vector2 Position;
        public readonly Action OnPopupClose;

        public ChatEntryMenuPopupData(Vector2 position, string copiedText, Action onPopupClose, UniTask? closeTask = null)
        {
            Position = position;
            CopiedText = copiedText;
            OnPopupClose = onPopupClose;
            CloseTask = closeTask;
        }
    }
}
