using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using System;
using System.Threading;
using CommunityData = DCL.Communities.GetCommunityResponse.CommunityData;

namespace DCL.Communities.CommunitiesCard.Events
{
    public class EventListController : CommunityFetchingControllerBase<EventDTO, EventListView>
    {
        private const int PAGE_SIZE = 20;

        private CommunityData? communityData = null;

        public EventListController(EventListView view) : base(view, PAGE_SIZE)
        {
        }

        protected override SectionFetchData<EventDTO> currentSectionFetchData { get; }

        protected override async UniTask<int> FetchDataAsync(CancellationToken ct)
        {
            return 0;
        }

        public void ShowEvents(CommunityData community, CancellationToken token)
        {
            cancellationToken = token;
            communityData = community;
            FetchNewDataAsync(token).Forget();
        }
    }
}
