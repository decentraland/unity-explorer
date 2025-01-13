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
        private readonly ISystemClipboard systemClipboard;

        public ChatEntryMenuPopupController(ViewFactoryMethod viewFactory, ISystemClipboard systemClipboard) : base(viewFactory)
        {
            this.systemClipboard = systemClipboard;
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
            inputData.Copy.Invoke(systemClipboard.Get());
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            inputData.CloseTask != null ? UniTask.WhenAll(inputData.CloseTask.Value) : UniTask.Never(ct);
    }


    public struct ChatEntryMenuPopupData
    {
        public readonly Action<string> Copy;
        public readonly UniTask? CloseTask;
        public readonly Vector2 Position;

        public ChatEntryMenuPopupData(Action<string> copy, Vector2 position, UniTask? closeTask = null)
        {
            Copy = copy;
            Position = position;
            CloseTask = closeTask;
        }
    }
}

