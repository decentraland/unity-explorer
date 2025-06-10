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

        private readonly EventListView view;
        private readonly SectionFetchData<EventDTO> eventsFetchData = new (PAGE_SIZE);

        private CommunityData? communityData = null;

        protected override SectionFetchData<EventDTO> currentSectionFetchData => eventsFetchData;

        public EventListController(EventListView view) : base(view, PAGE_SIZE)
        {
            this.view = view;
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
            return 0;
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
