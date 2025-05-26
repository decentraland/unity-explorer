using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Friends;
using DCL.InWorldCamera.CameraReelGallery;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.PhotoDetail;
using DCL.UI;
using DCL.Utilities;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardController : ControllerBase<CommunityCardView, CommunityCardParameter>
    {
        private static readonly int BG_SHADER_COLOR_1 = Shader.PropertyToID("_Color1");

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly IMVCManager mvcManager;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly ViewDependencies viewDependencies;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly ICommunitiesDataProvider communitiesDataProvider;
        private readonly IWebRequestController webRequestController;

        private ImageController? imageController;
        private CameraReelGalleryController? cameraReelGalleryController;
        private MembersListController? membersListController;
        private CancellationTokenSource photosSectionCancellationTokenSource = new ();
        private CancellationTokenSource membersSectionCancellationTokenSource = new ();
        private CancellationTokenSource placesSectionCancellationTokenSource = new ();
        private CancellationTokenSource loadCommunityDataCancellationTokenSource = new ();

        private GetCommunityResponse.CommunityData communityData;

        public CommunityCardController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            ViewDependencies viewDependencies,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ICommunitiesDataProvider communitiesDataProvider,
            IWebRequestController webRequestController)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.viewDependencies = viewDependencies;
            this.friendServiceProxy = friendServiceProxy;
            this.communitiesDataProvider = communitiesDataProvider;
            this.webRequestController = webRequestController;
        }

        public override void Dispose()
        {
            if (viewInstance != null)
            {
                viewInstance.SectionChanged -= OnSectionChanged;
                viewInstance.OpenWizard -= OpenCommunityWizard;
            }

            photosSectionCancellationTokenSource.SafeCancelAndDispose();
            membersSectionCancellationTokenSource.SafeCancelAndDispose();
            placesSectionCancellationTokenSource.SafeCancelAndDispose();

            cameraReelGalleryController?.Dispose();
            membersListController?.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.SectionChanged += OnSectionChanged;
            viewInstance.OpenWizard += OpenCommunityWizard;

            cameraReelGalleryController = new CameraReelGalleryController(viewInstance.CameraReelGalleryConfigs.CameraReelGalleryView, cameraReelStorageService, cameraReelScreenshotsStorage,
                new ReelGalleryConfigParams(viewInstance.CameraReelGalleryConfigs.GridLayoutFixedColumnCount, viewInstance.CameraReelGalleryConfigs.ThumbnailHeight,
                    viewInstance.CameraReelGalleryConfigs.ThumbnailWidth, false, false), false);
            cameraReelGalleryController.ThumbnailClicked += ThumbnailClicked;

            membersListController = new MembersListController(viewInstance.MembersListView, viewDependencies, mvcManager, friendServiceProxy, communitiesDataProvider);

            imageController = new ImageController(viewInstance.CommunityThumbnail, webRequestController);

            Color.RGBToHSV(viewInstance.BackgroundColor, out float h, out float s, out float v);
            viewInstance.BackgroundImage.material.SetColor(BG_SHADER_COLOR_1, Color.HSVToRGB(h, s, Mathf.Clamp01(v - 0.3f)));
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
                communityData = response.community;

                viewInstance.SetLoadingState(false);

                viewInstance.ConfigureCommunity(communityData, imageController);

                viewInstance.ToggleUIListeners(true);
            }
        }

        protected override void OnViewClose()
        {
            photosSectionCancellationTokenSource.SafeCancelAndDispose();
            membersSectionCancellationTokenSource.SafeCancelAndDispose();
            placesSectionCancellationTokenSource.SafeCancelAndDispose();
            loadCommunityDataCancellationTokenSource.SafeCancelAndDispose();
            viewInstance!.ToggleUIListeners(false);

            membersListController?.Reset();
        }

        private void ThumbnailClicked(List<CameraReelResponseCompact> reels, int index, Action<CameraReelResponseCompact> reelDeleteIntention) =>
            mvcManager.ShowAsync(PhotoDetailController.IssueCommand(new PhotoDetailParameter(reels, index, false, reelDeleteIntention)));

        private void OnSectionChanged(CommunityCardView.Sections section)
        {
            switch (section)
            {
                case CommunityCardView.Sections.PHOTOS:
                    photosSectionCancellationTokenSource = photosSectionCancellationTokenSource.SafeRestart();
                    cameraReelGalleryController!.ShowCommunityGalleryAsync(communityData.id, communityData.places, photosSectionCancellationTokenSource.Token).Forget();
                    break;
                case CommunityCardView.Sections.MEMBERS:
                    membersSectionCancellationTokenSource = membersSectionCancellationTokenSource.SafeRestart();
                    membersListController!.ShowMembersListAsync(communityData.id, communityData.role == CommunityMemberRole.Owner, membersSectionCancellationTokenSource.Token);
                    break;
                case CommunityCardView.Sections.PLACES:
                    placesSectionCancellationTokenSource = placesSectionCancellationTokenSource.SafeRestart();
                    break;
            }
        }

        private void OpenCommunityWizard()
        {
            throw new NotImplementedException();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.CloseButton.OnClickAsync(ct), viewInstance!.BackgroundCloseButton.OnClickAsync(ct));
    }
}
