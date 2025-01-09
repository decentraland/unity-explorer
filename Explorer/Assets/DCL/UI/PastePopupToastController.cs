using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.UI
{
    public class PastePopupToastController : ControllerBase<PastePopupToastView,PastePopupToastData>
    {
        private readonly ISystemClipboard systemClipboard;

        public PastePopupToastController(ViewFactoryMethod viewFactory, ISystemClipboard systemClipboard) : base(viewFactory)
        {
            this.systemClipboard = systemClipboard;
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
            inputData.Paste.Invoke(systemClipboard.Get());
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            inputData.CloseTask != null ? UniTask.WhenAll(inputData.CloseTask.Value) : UniTask.Never(ct);
    }


    public struct PastePopupToastData
    {
        public readonly Action<string> Paste;
        public readonly UniTask? CloseTask;
        public readonly Vector2 Position;

        public PastePopupToastData(Action<string> paste, Vector2 position, UniTask? closeTask = null)
        {
            Paste = paste;
            Position = position;
            CloseTask = closeTask;
        }
    }
}
