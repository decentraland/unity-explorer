using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;

namespace DCL.Communities.EventInfo
{
    public class EventInfoController : ControllerBase<EventInfoView, EventInfoParameter>
    {
        private readonly IWebRequestController webRequestController;
        private readonly IMVCManager mvcManager;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public EventInfoController(ViewFactoryMethod viewFactory,
            IWebRequestController webRequestController,
            IMVCManager mvcManager)
            : base(viewFactory)
        {
            this.webRequestController = webRequestController;
            this.mvcManager = mvcManager;
        }

        public override void Dispose()
        {
            if (viewInstance == null) return;

            viewInstance.InterestedButtonClicked -= OnInterestedButtonClicked;
            viewInstance.EventShareButtonClicked -= OnEventShareButtonClicked;
            viewInstance.EventCopyLinkButtonClicked -= OnEventCopyLinkButtonClicked;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetCloseTasks());

        protected override void OnViewInstantiated()
        {
            viewInstance!.Configure(mvcManager, webRequestController);

            viewInstance.InterestedButtonClicked += OnInterestedButtonClicked;
            viewInstance.EventShareButtonClicked += OnEventShareButtonClicked;
            viewInstance.EventCopyLinkButtonClicked += OnEventCopyLinkButtonClicked;
        }

        protected override void OnBeforeViewShow()
        {
            viewInstance!.ConfigureEventData(inputData.EventData);
        }

        private void OnEventCopyLinkButtonClicked(IEventDTO obj)
        {
            throw new NotImplementedException();
        }

        private void OnEventShareButtonClicked(IEventDTO obj)
        {
            throw new NotImplementedException();
        }

        private void OnInterestedButtonClicked(IEventDTO obj)
        {
            throw new NotImplementedException();
        }
    }
}
