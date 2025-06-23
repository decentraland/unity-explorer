using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;

namespace DCL.Communities.EventInfo
{
    public class EventInfoController : ControllerBase<EventInfoView, EventInfoParameter>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public EventInfoController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
