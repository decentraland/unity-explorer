using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using System.Threading;
using CommunityData = DCL.Communities.CommunitiesDataProvider.DTOs.GetCommunityResponse.CommunityData;

namespace DCL.Communities.CommunitiesCard.Announcements
{
    public class AnnouncementsSectionController : CommunityFetchingControllerBase<CommunityPost, AnnouncementsSectionView>
    {
        private const int PAGE_SIZE = 10;
        private const string COMMUNITY_ANNOUNCEMENTS_FETCH_ERROR_MESSAGE = "There was an error fetching the community announcements. Please try again.";

        private readonly AnnouncementsSectionView view;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider communitiesDataProvider;
        private readonly SectionFetchData<CommunityPost> communityPostsFetchData = new (PAGE_SIZE);

        protected override SectionFetchData<CommunityPost> currentSectionFetchData => communityPostsFetchData;

        private CommunityData? communityData;
        private bool userCanModify;

        public AnnouncementsSectionController(
            AnnouncementsSectionView view,
            CommunitiesDataProvider.CommunitiesDataProvider communitiesDataProvider) : base (view, PAGE_SIZE)
        {
            this.view = view;
            this.communitiesDataProvider = communitiesDataProvider;

            view.InitList(cancellationToken);
        }

        public override void Reset()
        {
            communityData = null;
            communityPostsFetchData.Reset();
            view.SetCanModify(false);
            base.Reset();
        }

        protected override async UniTask<int> FetchDataAsync(CancellationToken ct)
        {
            if (communityData == null)
                return 0;

            SectionFetchData<CommunityPost> announcementsData = currentSectionFetchData;

            Result<GetCommunityPostsResponse> response = await communitiesDataProvider.GetCommunityPostsAsync(communityData.Value.id, announcementsData.PageNumber, PAGE_SIZE, ct)
                                                                                      .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return 0;

            if (!response.Success)
            {
                announcementsData.PageNumber--;
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(COMMUNITY_ANNOUNCEMENTS_FETCH_ERROR_MESSAGE));
                return announcementsData.TotalToFetch;
            }

            foreach (var announcement in response.Value.data.posts)
                if (!announcementsData.Items.Contains(announcement))
                    announcementsData.Items.Add(announcement);

            return response.Value.data.total;
        }

        public void ShowAnnouncements(CommunityData community, CancellationToken token)
        {
            cancellationToken = token;

            if (communityData is not null && community.id.Equals(communityData.Value.id))
                return;

            communityData = community;
            userCanModify = communityData.Value.role is CommunityMemberRole.moderator or CommunityMemberRole.owner;
            view.SetCanModify(userCanModify);
            view.SetCommunityData(community);

            FetchNewDataAsync(token).Forget();
        }
    }
}
