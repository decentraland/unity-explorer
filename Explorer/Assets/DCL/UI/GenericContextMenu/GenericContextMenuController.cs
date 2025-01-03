using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.UI.GenericContextMenu
{
    public class GenericContextMenuController : ControllerBase<GenericContextMenuView, GenericContextMenuParameter>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private bool isClosing;

        public GenericContextMenuController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.BackgroundCloseButtonClicked += BackgroundCloseButtonClicked;
        }

        protected override void OnViewShow()
        {
            isClosing = false;

        }

        private void BackgroundCloseButtonClicked() => isClosing = true;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(UniTask.WaitUntil(() => isClosing, cancellationToken: ct));
    }
}
