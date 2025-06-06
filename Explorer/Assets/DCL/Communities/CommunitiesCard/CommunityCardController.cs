using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.InWorldCamera.CameraReelGallery;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.PhotoDetail;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardController : ControllerBase<CommunityCardView, CommunityCardParameter>
    {
        private static readonly int BG_SHADER_COLOR_1 = Shader.PropertyToID("_Color1");

        private const string JOIN_COMMUNITY_ERROR_TEXT = "There was an error joining the community. Please try again.";
        private const string LEAVE_COMMUNITY_ERROR_TEXT = "There was an error leaving the community. Please try again.";
        private const int WARNING_NOTIFICATION_DURATION_MS = 3000;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly IMVCManager mvcManager;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly ICommunitiesDataProvider communitiesDataProvider;
        private readonly IWebRequestController webRequestController;
        private readonly WarningNotificationView inWorldWarningNotificationView;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

        private ImageController? imageController;
        private CameraReelGalleryController? cameraReelGalleryController;
        private MembersListController? membersListController;
        private CancellationTokenSource sectionCancellationTokenSource = new ();
        private CancellationTokenSource loadCommunityDataCancellationTokenSource = new ();
        private CancellationTokenSource communityOperationsCancellationTokenSource = new ();

        private GetCommunityResponse.CommunityData communityData;

        public CommunityCardController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ICommunitiesDataProvider communitiesDataProvider,
            IWebRequestController webRequestController,
            WarningNotificationView inWorldWarningNotificationView,
            ProfileRepositoryWrapper profileDataProvider)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.friendServiceProxy = friendServiceProxy;
            this.communitiesDataProvider = communitiesDataProvider;
            this.webRequestController = webRequestController;
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;
            this.profileRepositoryWrapper = profileDataProvider;
        }

        public override void Dispose()
        {
            if (viewInstance != null)
            {
                viewInstance.SectionChanged -= OnSectionChanged;
                viewInstance.OpenWizardRequested -= OnOpenCommunityWizard;
                viewInstance.JoinCommunity -= JoinCommunity;
                viewInstance.LeaveCommunityRequested -= LeaveCommunityRequested;
            }

            sectionCancellationTokenSource.SafeCancelAndDispose();
            communityOperationsCancellationTokenSource.SafeCancelAndDispose();

            if (cameraReelGalleryController != null)
                cameraReelGalleryController.ThumbnailClicked -= OnThumbnailClicked;

            cameraReelGalleryController?.Dispose();
            membersListController?.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.SectionChanged += OnSectionChanged;
            viewInstance.OpenWizardRequested += OnOpenCommunityWizard;
            viewInstance.JoinCommunity += JoinCommunity;
            viewInstance.LeaveCommunityRequested += LeaveCommunityRequested;

            cameraReelGalleryController = new CameraReelGalleryController(viewInstance.CameraReelGalleryConfigs.PhotosView.GalleryView, cameraReelStorageService, cameraReelScreenshotsStorage,
                new ReelGalleryConfigParams(viewInstance.CameraReelGalleryConfigs.GridLayoutFixedColumnCount, viewInstance.CameraReelGalleryConfigs.ThumbnailHeight,
                    viewInstance.CameraReelGalleryConfigs.ThumbnailWidth, false, false), false);
            cameraReelGalleryController.ThumbnailClicked += OnThumbnailClicked;

            membersListController = new MembersListController(viewInstance.MembersListView,
                profileRepositoryWrapper,
                mvcManager,
                friendServiceProxy,
                communitiesDataProvider,
                inWorldWarningNotificationView);

            imageController = new ImageController(viewInstance.CommunityThumbnail, webRequestController);

            viewInstance.SetCardBackgroundColor(viewInstance.BackgroundColor, BG_SHADER_COLOR_1);
        }

        protected override void OnViewShow()
        {
            loadCommunityDataCancellationTokenSource = loadCommunityDataCancellationTokenSource.SafeRestart();
            LoadCommunityDataAsync(loadCommunityDataCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid LoadCommunityDataAsync(CancellationToken ct)
            {
                viewInstance!.SetLoadingState(true);

                GetCommunityResponse response = await communitiesDataProvider.GetCommunityAsync(inputData.CommunityId, ct);
                communityData = response.data;

                viewInstance.SetLoadingState(false);

                viewInstance.ConfigureCommunity(communityData, imageController);

                viewInstance.ResetToggle();
            }
        }

        protected override void OnViewClose()
        {
            sectionCancellationTokenSource.SafeCancelAndDispose();
            loadCommunityDataCancellationTokenSource.SafeCancelAndDispose();
            communityOperationsCancellationTokenSource.SafeCancelAndDispose();

            membersListController?.Reset();
        }

        private void OnThumbnailClicked(List<CameraReelResponseCompact> reels, int index, Action<CameraReelResponseCompact> reelDeleteIntention) =>
            mvcManager.ShowAsync(PhotoDetailController.IssueCommand(new PhotoDetailParameter(reels, index, false, reelDeleteIntention)));

        private void OnSectionChanged(CommunityCardView.Sections section)
        {
            sectionCancellationTokenSource = sectionCancellationTokenSource.SafeRestart();
            switch (section)
            {
                case CommunityCardView.Sections.PHOTOS:
                    cameraReelGalleryController!.ShowCommunityGalleryAsync(communityData.id, communityData.places, sectionCancellationTokenSource.Token).Forget();
                    break;
                case CommunityCardView.Sections.MEMBERS:
                    membersListController!.ShowMembersList(communityData, sectionCancellationTokenSource.Token);
                    break;
                case CommunityCardView.Sections.PLACES:
                    break;
            }
        }

        private void OnOpenCommunityWizard()
        {
            //TODO: Open the community wizard
            throw new NotImplementedException();
        }

        private void JoinCommunity()
        {
            communityOperationsCancellationTokenSource = communityOperationsCancellationTokenSource.SafeRestart();
            JoinCommunityAsync(communityOperationsCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid JoinCommunityAsync(CancellationToken ct)
            {
                Result<bool> result = await communitiesDataProvider.JoinCommunityAsync(communityData.id, ct)
                                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(JOIN_COMMUNITY_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, ct);
                    return;
                }

                viewInstance!.ConfigureInteractionButtons(CommunityMemberRole.member);
            }
        }

        private void LeaveCommunityRequested()
        {
            communityOperationsCancellationTokenSource = communityOperationsCancellationTokenSource.SafeRestart();
            LeaveCommunityAsync(communityOperationsCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid LeaveCommunityAsync(CancellationToken ct)
            {
                Result<bool> result = await communitiesDataProvider.LeaveCommunityAsync(communityData.id, ct)
                                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(LEAVE_COMMUNITY_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, ct);
                    return;
                }

                viewInstance!.ConfigureInteractionButtons(CommunityMemberRole.none);
            }
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetClosingTasks(ct));
    }
}
