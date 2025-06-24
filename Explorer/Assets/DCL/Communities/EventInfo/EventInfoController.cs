using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;

namespace DCL.Communities.EventInfo
{
    public class EventInfoController : ControllerBase<EventInfoView, EventInfoParameter>
    {
        private readonly IWebRequestController webRequestController;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public EventInfoController(ViewFactoryMethod viewFactory,
            IWebRequestController webRequestController)
            : base(viewFactory)
        {
            this.webRequestController = webRequestController;
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void OnBeforeViewShow()
        {
            viewInstance!.ConfigureEventData(inputData.eventData, webRequestController);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetCloseTasks());
    }
}
