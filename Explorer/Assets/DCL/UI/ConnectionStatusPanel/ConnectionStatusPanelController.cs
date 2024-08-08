using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;

namespace DCL.UI.ConnectionStatusPanel
{
    public partial class ConnectionStatusPanelController : ControllerBase<ConnectionStatusPanelView>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ConnectionStatusPanelController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {

        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
