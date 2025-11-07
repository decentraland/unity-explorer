using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.Web3.Identities;
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
        private bool isCreationAllowed;

        public AnnouncementsSectionController(
            AnnouncementsSectionView view,
            CommunitiesDataProvider.CommunitiesDataProvider communitiesDataProvider,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository) : base (view, PAGE_SIZE)
        {
            this.view = view;
            this.communitiesDataProvider = communitiesDataProvider;

            InitializeViewAsync(identityCache, profileRepository, profileRepositoryWrapper, cancellationToken).Forget();

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
            view.SetAllowCreation(false);

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

            if (currentAnnouncementsFetchData.PageNumber == 1 && isCreationAllowed)
            {
                currentAnnouncementsFetchData.Items.Add(new CommunityPost { type = CommunityPostType.CREATION_INPUT });

                if (response.Value.data.total > 0)
                    currentAnnouncementsFetchData.Items.Add(new CommunityPost { type = CommunityPostType.SEPARATOR });
            }

            foreach (var announcement in response.Value.data.posts)
                currentAnnouncementsFetchData.Items.Add(announcement);

            return response.Value.data.total;
        }

        public void ShowAnnouncements(CommunityData community, CancellationToken token)
        {
            cancellationToken = token;

            if (communityData is not null && community.id.Equals(communityData.Value.id))
            {
                RefreshGrid(true);
                return;
            }

            communityData = community;
            isCreationAllowed = communityData.Value.role is CommunityMemberRole.moderator or CommunityMemberRole.owner;
            view.SetAllowCreation(isCreationAllowed);
            FetchNewDataAsync(token).Forget();
        }

        private async UniTaskVoid InitializeViewAsync(
            IWeb3IdentityCache identity,
            IProfileRepository profileRepo,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            CancellationToken ct)
        {
            view.InitList();

            if (identity.Identity == null)
            {
                ReportHub.LogError(ReportCategory.PROFILE, "Cannot setup own profile. Identity is null.");
                return;
            }

            Profile? profile = await profileRepo.GetAsync(identity.Identity!.Address, ct);
            view.SetProfile(profile, profileRepositoryWrapper);
        }

        private void CreateAnnouncement(string announcementContent)
        {
            if (communityData == null)
                return;

            CreateAnnouncementAsync(announcementContent, CancellationToken.None).Forget();
            return;

            async UniTaskVoid CreateAnnouncementAsync(string content, CancellationToken ct)
            {
                Result<CreateCommunityPostResponse> response = await communitiesDataProvider.CreateCommunityPostAsync(communityData.Value.id, content, ct)
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

        private void DeleteAnnouncement(string announcementId)
        {
            if (communityData == null)
                return;

            DeleteAnnouncementAsync(announcementId, CancellationToken.None).Forget();
            return;

            async UniTaskVoid DeleteAnnouncementAsync(string postId, CancellationToken ct)
            {
                Result<bool> response = await communitiesDataProvider.DeleteCommunityPostAsync(communityData.Value.id, postId, CancellationToken.None)
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
