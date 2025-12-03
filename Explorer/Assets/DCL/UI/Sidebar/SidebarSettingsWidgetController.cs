using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.UI.Sidebar
{
    public class SidebarSettingsWidgetController : ControllerBase<SidebarSettingsWidgetView>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private UniTaskCompletionSource? closeViewTask;

        public SidebarSettingsWidgetController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeViewTask = new UniTaskCompletionSource();
            await closeViewTask.Task.AttachExternalCancellation(ct).SuppressCancellationThrow();
        }

        protected override void OnViewClose()
        {
            closeViewTask?.TrySetCanceled();
        }
    }
}

