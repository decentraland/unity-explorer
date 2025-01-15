using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;

namespace DCL.UI.Controls
{
    public class ControlsPanelController: ControllerBase<ControlsPanelView>
    {
        public ControlsPanelController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            return UniTask.WhenAny(viewInstance!.closeButton.OnClickAsync(ct));
        }
    }
}
