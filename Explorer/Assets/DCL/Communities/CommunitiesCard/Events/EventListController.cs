using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using Utility.Types;
using CommunityData = DCL.Communities.GetCommunityResponse.CommunityData;
using PlaceAndEventDTO = DCL.EventsApi.CommunityEventsDTO.PlaceAndEventDTO;

namespace DCL.Communities.CommunitiesCard.Events
{
    public class EventListController : CommunityFetchingControllerBase<PlaceAndEventDTO, EventListView>
    {
        private const int PAGE_SIZE = 20;

        private readonly EventListView view;
        private readonly IEventsApiService eventsApiService;
        private readonly SectionFetchData<PlaceAndEventDTO> eventsFetchData = new (PAGE_SIZE);

        private CommunityData? communityData = null;

        protected override SectionFetchData<PlaceAndEventDTO> currentSectionFetchData => eventsFetchData;

        public EventListController(EventListView view,
            IEventsApiService eventsApiService,
            IWebRequestController webRequestController,
            IMVCManager mvcManager) : base(view, PAGE_SIZE)
        {
            this.view = view;
            this.eventsApiService = eventsApiService;
            view.InitList(() => currentSectionFetchData, webRequestController, mvcManager, cancellationToken);

            view.OpenWizardRequested += OnOpenWizardRequested;
            view.MainButtonClicked += OnMainButtonClicked;
            view.JumpInButtonClicked += OnJumpInButtonClicked;
            view.InterestedButtonClicked += OnInterestedButtonClicked;
            view.EventShareButtonClicked += OnEventShareButtonClicked;
            view.EventCopyLinkButtonClicked += OnEventCopyLinkButtonClicked;
        }

        public override void Dispose()
        {
            view.OpenWizardRequested -= OnOpenWizardRequested;
            view.JumpInButtonClicked -= OnJumpInButtonClicked;
            view.InterestedButtonClicked -= OnInterestedButtonClicked;
            view.EventShareButtonClicked -= OnEventShareButtonClicked;
            view.EventCopyLinkButtonClicked -= OnEventCopyLinkButtonClicked;

            base.Dispose();
        }

        private void OnEventCopyLinkButtonClicked(EventDTO eventData)
        {
            throw new NotImplementedException();
        }

        private void OnEventShareButtonClicked(EventDTO eventData)
        {
            throw new NotImplementedException();
        }

        private void OnInterestedButtonClicked(EventDTO eventData)
        {
            throw new NotImplementedException();
        }

        private void OnJumpInButtonClicked(EventDTO eventData)
        {
            throw new NotImplementedException();
        }

        private void OnMainButtonClicked(EventDTO eventData)
        {
            throw new NotImplementedException();
        }

        public override void Reset()
        {
            communityData = null;
            eventsFetchData.Reset();
            view.SetCanModify(false);
            base.Reset();
        }

        private void OnOpenWizardRequested()
        {
            //TODO: open community wizard
            throw new NotImplementedException();
        }

        protected override async UniTask<int> FetchDataAsync(CancellationToken ct)
        {
            Result<CommunityEventsDTO> response = await eventsApiService.GetEventsByCommunityAsync(communityData!.Value.id, eventsFetchData.pageNumber, PAGE_SIZE, ct)
                                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!response.Success)
            {
                //If the request fails, we restore the previous page number in order to retry the same request next time
                eventsFetchData.pageNumber--;
                return eventsFetchData.totalToFetch;
            }

            eventsFetchData.members.AddRange(response.Value.data);

            return response.Value.totalAmount;
        }

        public void ShowEvents(CommunityData community, CancellationToken token)
        {
            cancellationToken = token;
            communityData = community;
            view.SetCanModify(community.role is CommunityMemberRole.owner or CommunityMemberRole.moderator);
            FetchNewDataAsync(token).Forget();
        }
    }
}
