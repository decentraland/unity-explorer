using Cysharp.Threading.Tasks;
using DCL.UI;
using MVC;
using System.Threading;

namespace DCL.InWorldCamera.UI
{
    /// <summary>
    /// Handles Shortcuts popup on the InWorldCamera HUD.
    /// </summary>
    public class ScreencaptureShortcutsController : ControllerBase<ElementWithCloseArea>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public ScreencaptureShortcutsController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        public async UniTask HideAsync(CancellationToken ct) =>
            await viewInstance.HideAsync(ct);
    }
}
