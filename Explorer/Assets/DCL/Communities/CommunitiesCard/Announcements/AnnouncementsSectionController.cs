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
using System.Collections.Generic;
using System.Threading;
using CommunityData = DCL.Communities.CommunitiesDataProvider.DTOs.GetCommunityResponse.CommunityData;

namespace DCL.Communities.CommunitiesCard.Announcements
{
    public class AnnouncementsSectionController : CommunityFetchingControllerBase<CommunityPost, AnnouncementsSectionView>
    {
        private const int PAGE_SIZE = 10;
        private const string COMMUNITY_ANNOUNCEMENTS_FETCH_ERROR_MESSAGE = "There was an error fetching the community announcements. Please try again.";
        private const string COMMUNITY_ANNOUNCEMENT_CREATION_ERROR_MESSAGE = "There was an error creating the community announcement. Please try again.";
        private const string COMMUNITY_ANNOUNCEMENT_DELETION_ERROR_MESSAGE = "There was an error deleting the community announcement. Please try again.";
        private const string COMMUNITY_ANNOUNCEMENT_LIKE_ERROR_MESSAGE = "There was an error sending the like status to the community announcement. Please try again.";

        private readonly AnnouncementsSectionView view;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider communitiesDataProvider;
        private readonly SectionFetchData<CommunityPost> currentAnnouncementsFetchData = new (PAGE_SIZE);

        protected override SectionFetchData<CommunityPost> currentSectionFetchData => currentAnnouncementsFetchData;

        private CommunityData? communityData;
        private bool lastFetchSucceeded;
        private bool isCreationAllowed;
        private bool isPosting;
        private readonly HashSet<string> announcementsUpdatingLikeStatus = new();

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
            view.LikeAnnouncementButtonClicked += LikeAnnouncement;
            view.UnlikeAnnouncementButtonClicked += UnlikeAnnouncement;
            view.DeleteAnnouncementButtonClicked += DeleteAnnouncement;
        }

        public override void Dispose()
        {
            view.CreateAnnouncementButtonClicked -= CreateAnnouncement;
            view.LikeAnnouncementButtonClicked -= LikeAnnouncement;
            view.UnlikeAnnouncementButtonClicked -= UnlikeAnnouncement;
            view.DeleteAnnouncementButtonClicked -= DeleteAnnouncement;

            base.Dispose();
        }

        public override void Reset()
        {
            communityData = null;
            currentAnnouncementsFetchData.Reset();
            view.SetAllowCreation(false);
            view.SetRole(CommunityMemberRole.none);

            base.Reset();
        }

        protected override async UniTask<int> FetchDataAsync(CancellationToken ct)
        {
            if (communityData == null)
                return 0;

            lastFetchSucceeded = false;
            Result<GetCommunityPostsResponse> response = await communitiesDataProvider.GetCommunityPostsAsync(communityData.Value.id, currentAnnouncementsFetchData.PageNumber, PAGE_SIZE, ct)
                                                                                      .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
            {
                currentAnnouncementsFetchData.PageNumber--;
                return 0;
            }

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

            lastFetchSucceeded = true;
            return response.Value.data.total;
        }

        public void ShowAnnouncements(CommunityData community, CancellationToken token)
        {
            cancellationToken = token;

            if (communityData is not null && community.id.Equals(communityData.Value.id) && lastFetchSucceeded)
            {
                RefreshGrid(true);
                return;
            }

            communityData = community;
            isCreationAllowed = communityData.Value.role is CommunityMemberRole.moderator or CommunityMemberRole.owner;
            isPosting = false;
            announcementsUpdatingLikeStatus.Clear();
            view.SetAllowCreation(isCreationAllowed);
            view.SetRole(communityData.Value.role);
            view.CleanCreationInput();
            view.SetCreationAsLoading(false);
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

            Profile? profile = await profileRepo.GetAsync(identity.Identity!.Address, ct, IProfileRepository.FetchBehaviour.DELAY_UNTIL_RESOLVED);
            view.SetProfile(profile, profileRepositoryWrapper);
        }

        private void CreateAnnouncement(string announcementContent)
        {
            if (communityData == null || isPosting)
                return;

            CreateAnnouncementAsync(announcementContent, cancellationToken).Forget();
            return;

            async UniTaskVoid CreateAnnouncementAsync(string content, CancellationToken ct)
            {
                isPosting = true;
                view.SetCreationAsLoading(true);
                Result<CreateCommunityPostResponse> response = await communitiesDataProvider.CreateCommunityPostAsync(communityData.Value.id, content, ct)
                                                                                            .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                isPosting = false;

                if (ct.IsCancellationRequested)
                    return;

                if (!response.Success)
                {
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(COMMUNITY_ANNOUNCEMENT_CREATION_ERROR_MESSAGE));
                    return;
                }

                currentAnnouncementsFetchData.Reset();
                FetchNewDataAsync(ct).Forget();
                RefreshGrid(true);
                view.CleanCreationInput();
                view.SetCreationAsLoading(false);
            }
        }

        private void LikeAnnouncement(string announcementId)
        {
            if (communityData == null || announcementsUpdatingLikeStatus.Contains(announcementId))
                return;

            LikeAnnouncementAsync(announcementId, cancellationToken).Forget();
            return;

            async UniTaskVoid LikeAnnouncementAsync(string postId, CancellationToken ct)
            {
                announcementsUpdatingLikeStatus.Add(postId);
                Result<bool> response = await communitiesDataProvider.LikeCommunityPostAsync(communityData.Value.id, postId, ct)
                                                                     .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                announcementsUpdatingLikeStatus.Remove(postId);

                if (ct.IsCancellationRequested)
                    return;

                if (!response.Success || !response.Value)
                {
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(COMMUNITY_ANNOUNCEMENT_LIKE_ERROR_MESSAGE));
                    return;
                }

                foreach (CommunityPost post in currentAnnouncementsFetchData.Items)
                {
                    if (post.id == postId)
                    {
                        post.isLikedByUser = true;
                        post.likesCount++;
                        break;
                    }
                }

                RefreshGrid(true);
            }
        }

        private void UnlikeAnnouncement(string announcementId)
        {
            if (communityData == null || announcementsUpdatingLikeStatus.Contains(announcementId))
                return;

            UnlikeAnnouncementAsync(announcementId, cancellationToken).Forget();
            return;

            async UniTaskVoid UnlikeAnnouncementAsync(string postId, CancellationToken ct)
            {
                announcementsUpdatingLikeStatus.Add(postId);
                Result<bool> response = await communitiesDataProvider.UnlikeCommunityPostAsync(communityData.Value.id, postId, ct)
                                                                     .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                announcementsUpdatingLikeStatus.Remove(postId);

                if (ct.IsCancellationRequested)
                    return;

                if (!response.Success || !response.Value)
                {
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(COMMUNITY_ANNOUNCEMENT_LIKE_ERROR_MESSAGE));
                    return;
                }

                foreach (CommunityPost post in currentAnnouncementsFetchData.Items)
                {
                    if (post.id == postId)
                    {
                        post.isLikedByUser = false;
                        post.likesCount--;
                        break;
                    }
                }

                RefreshGrid(true);
            }
        }

        private void DeleteAnnouncement(string announcementId)
        {
            if (communityData == null)
                return;

            DeleteAnnouncementAsync(announcementId, cancellationToken).Forget();
            return;

            async UniTaskVoid DeleteAnnouncementAsync(string postId, CancellationToken ct)
            {
                Result<bool> response = await communitiesDataProvider.DeleteCommunityPostAsync(communityData.Value.id, postId, ct)
                                                                     .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!response.Success || !response.Value)
                {
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(COMMUNITY_ANNOUNCEMENT_DELETION_ERROR_MESSAGE));
                    return;
                }

                currentAnnouncementsFetchData.Reset();
                FetchNewDataAsync(ct).Forget();
                RefreshGrid(true);
            }
        }
    }
}
