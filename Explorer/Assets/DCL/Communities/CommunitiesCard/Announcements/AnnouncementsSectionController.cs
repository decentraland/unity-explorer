using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.UI.Profiles.Helpers;
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
        private readonly SectionFetchData<CommunityPost> currentAnnouncementsFetchData = new (PAGE_SIZE);

        protected override SectionFetchData<CommunityPost> currentSectionFetchData => currentAnnouncementsFetchData;

        private CommunityData? communityData;

        public AnnouncementsSectionController(
            AnnouncementsSectionView view,
            CommunitiesDataProvider.CommunitiesDataProvider communitiesDataProvider,
            ProfileRepositoryWrapper profileRepositoryWrapper) : base (view, PAGE_SIZE)
        {
            this.view = view;
            this.communitiesDataProvider = communitiesDataProvider;

            view.InitList(profileRepositoryWrapper, cancellationToken);

            view.CreateAnnouncementButtonClicked += CreateAnnouncement;
            view.DeleteAnnouncementButtonClicked += DeleteAnnouncement;
        }

        public override void Dispose()
        {
            view.CreateAnnouncementButtonClicked -= CreateAnnouncement;
            view.DeleteAnnouncementButtonClicked -= DeleteAnnouncement;

            base.Dispose();
        }

        public override void Reset()
        {
            communityData = null;
            currentAnnouncementsFetchData.Reset();

            base.Reset();
        }

        protected override async UniTask<int> FetchDataAsync(CancellationToken ct)
        {
            if (communityData == null)
                return 0;

            Result<GetCommunityPostsResponse> response = await communitiesDataProvider.GetCommunityPostsAsync(communityData.Value.id, currentAnnouncementsFetchData.PageNumber, PAGE_SIZE, ct)
                                                                                      .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return 0;

            if (!response.Success)
            {
                currentAnnouncementsFetchData.PageNumber--;
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(COMMUNITY_ANNOUNCEMENTS_FETCH_ERROR_MESSAGE));
                return currentAnnouncementsFetchData.TotalToFetch;
            }

            foreach (var announcement in response.Value.data.posts)
                currentAnnouncementsFetchData.Items.Add(announcement);

            return response.Value.data.total;
        }

        public void ShowAnnouncements(CommunityData community, CancellationToken token)
        {
            cancellationToken = token;

            if (communityData is not null && community.id.Equals(communityData.Value.id))
                return;

            communityData = community;
            FetchNewDataAsync(token).Forget();
        }

        private void CreateAnnouncement()
        {
            if (communityData == null)
                return;

            CreateAnnouncementAsync($"Test post {UnityEngine.Random.Range(0, 10000)}", CancellationToken.None).Forget();
            return;

            async UniTaskVoid CreateAnnouncementAsync(string content, CancellationToken ct)
            {
                Result<CreateCommunityPostResponse> response = await communitiesDataProvider.CreateCommunityPostAsync(communityData.Value.id, content, CancellationToken.None)
                                                                                            .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!response.Success)
                    return;

                currentAnnouncementsFetchData.Reset();
                FetchNewDataAsync(ct).Forget();
                RefreshGrid(true);
            }
        }

        private void DeleteAnnouncement(string communityId, string announcementId)
        {
            DeleteAnnouncementAsync(communityId, announcementId, CancellationToken.None).Forget();
            return;

            async UniTaskVoid DeleteAnnouncementAsync(string commId, string postId, CancellationToken ct)
            {
                Result<bool> response = await communitiesDataProvider.DeleteCommunityPostAsync(commId, postId, CancellationToken.None)
                                                                     .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!response.Success || !response.Value)
                    return;

                currentAnnouncementsFetchData.Reset();
                FetchNewDataAsync(ct).Forget();
                RefreshGrid(true);
            }
        }
    }
}
