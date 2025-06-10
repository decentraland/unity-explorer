using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility.Types;
using CommunityData = DCL.Communities.GetCommunityResponse.CommunityData;

namespace DCL.Communities.CommunitiesCard.Events
{
    public class EventListController : CommunityFetchingControllerBase<EventDTO, EventListView>
    {
        private const int PAGE_SIZE = 20;

        private readonly EventListView view;
        private readonly IEventsApiService eventsApiService;
        private readonly SectionFetchData<EventDTO> eventsFetchData = new (PAGE_SIZE);

        private CommunityData? communityData = null;

        protected override SectionFetchData<EventDTO> currentSectionFetchData => eventsFetchData;

        public EventListController(EventListView view,
            IEventsApiService eventsApiService) : base(view, PAGE_SIZE)
        {
            this.view = view;
            this.eventsApiService = eventsApiService;
            view.InitList(() => currentSectionFetchData);

            view.OpenWizardRequested += OnOpenWizardRequested;
        }

        public override void Dispose()
        {
            view.OpenWizardRequested -= OnOpenWizardRequested;

            base.Dispose();
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
