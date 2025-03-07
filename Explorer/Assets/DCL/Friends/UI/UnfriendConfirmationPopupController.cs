using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.Profiles;
using DCL.Web3;
using MVC;
using System.Threading;
using Utility;

namespace DCL.Friends.UI
{
    public class UnfriendConfirmationPopupController : ControllerBase<UnfriendConfirmationPopupView, UnfriendConfirmationPopupController.Params>
    {
        private readonly IFriendsService friendsService;
        private readonly IProfileRepository profileRepository;
        private readonly IProfileThumbnailCache profileThumbnailCache;
        private UniTaskCompletionSource? lifeCycleTask;
        private CancellationTokenSource? unfriendCancellationToken;
        private CancellationTokenSource? fetchProfileCancellationToken;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public UnfriendConfirmationPopupController(ViewFactoryMethod viewFactory,
            IFriendsService friendsService,
            IProfileRepository profileRepository,
            IProfileThumbnailCache profileThumbnailCache) : base(viewFactory)
        {
            this.friendsService = friendsService;
            this.profileRepository = profileRepository;
            this.profileThumbnailCache = profileThumbnailCache;
        }

        public override void Dispose()
        {
            base.Dispose();

            unfriendCancellationToken.SafeCancelAndDispose();
            fetchProfileCancellationToken.SafeCancelAndDispose();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            lifeCycleTask = new UniTaskCompletionSource();
            await lifeCycleTask.Task;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.CancelButton.onClick.AddListener(Close);
            viewInstance.ConfirmButton.onClick.AddListener(Unfriend);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            fetchProfileCancellationToken = fetchProfileCancellationToken.SafeRestart();
            FetchProfileAndFillDescriptionAsync(fetchProfileCancellationToken.Token).Forget();
            return;

            async UniTaskVoid FetchProfileAndFillDescriptionAsync(CancellationToken ct)
            {
                viewInstance!.ProfileImage.IsLoading = true;
                viewInstance!.DescriptionLabel.text = "Are you sure you want to unfriend?";

                Profile? profile = await profileRepository.GetAsync(inputData.UserId, ct);

                if (profile == null) return;

                viewInstance!.DescriptionLabel.text = $"Are you sure you want to unfriend {profile.Name}?";

                await viewInstance!.ProfileImage.LoadThumbnailSafeAsync(profileThumbnailCache, inputData.UserId, profile.Avatar.FaceSnapshotUrl, ct);
            }
        }

        private void Close()
        {
            lifeCycleTask?.TrySetResult();
        }

        private void Unfriend()
        {
            unfriendCancellationToken = unfriendCancellationToken.SafeRestart();
            UnfriendAsync(unfriendCancellationToken.Token).Forget();
            return;

            async UniTaskVoid UnfriendAsync(CancellationToken ct)
            {
                await friendsService.DeleteFriendshipAsync(inputData.UserId, ct);

                Close();
            }
        }

        public struct Params
        {
            public Web3Address UserId { get; set; }
        }
    }
}
