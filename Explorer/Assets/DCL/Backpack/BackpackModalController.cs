using Cysharp.Threading.Tasks;
using DCL.UI;
using MVC;
using System.Threading;

namespace DCL.Backpack
{
    /// <summary>
    ///     Shows the backpack on its own over the panel that opened it, borrowing the single backpack view from the explore panel.
    /// </summary>
    public class BackpackModalController : ControllerBase<BackpackModalView, BackpackModalParameter>
    {
        private readonly BackpackController backpack;

        private UniTaskCompletionSource? closeIntent;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public BackpackModalController(ViewFactoryMethod viewFactory, BackpackController backpack) : base(viewFactory)
        {
            this.backpack = backpack;
        }

        protected override void OnBeforeViewShow() =>
            backpack.AttachTo(viewInstance!.BackpackHost, compact: true);

        protected override void OnViewShow()
        {
            backpack.CloseRequested += OnBackpackCloseRequested;
            backpack.Activate();
            backpack.Toggle(inputData.Section);
        }

        protected override void OnViewClose()
        {
            backpack.CloseRequested -= OnBackpackCloseRequested;

            // A fullscreen panel closes the popups without waiting for them, so by the time this runs the explore panel may already have claimed the view back
            if (backpack.CurrentHost != viewInstance!.BackpackHost) return;

            backpack.Deactivate();
            backpack.AttachToHome();
        }

        private void OnBackpackCloseRequested() =>
            closeIntent?.TrySetResult();

        // The close control is the backpack's own, shown only while it is hosted here
        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeIntent?.TrySetCanceled(ct);
            closeIntent = new UniTaskCompletionSource();

            await closeIntent.Task.AttachExternalCancellation(ct);
        }
    }

    public readonly struct BackpackModalParameter
    {
        public readonly BackpackSections Section;

        public BackpackModalParameter(BackpackSections section)
        {
            Section = section;
        }
    }
}
