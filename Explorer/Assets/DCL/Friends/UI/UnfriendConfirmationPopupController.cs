using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Utilities.Extensions;
using DCL.Web3;
using MVC;
using System.Threading;
using Utility;
using Utility.Types;

namespace DCL.Friends.UI
{
    public class UnfriendConfirmationPopupController : ControllerBase<UnfriendConfirmationPopupView, UnfriendConfirmationPopupController.Params>
    {
        private readonly IFriendsService friendsService;
        private readonly IProfileRepository profileRepository;
        private readonly ViewDependencies viewDependencies;
        private UniTaskCompletionSource? lifeCycleTask;
        private CancellationTokenSource? unfriendCancellationToken;
        private CancellationTokenSource? fetchProfileCancellationToken;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public UnfriendConfirmationPopupController(ViewFactoryMethod viewFactory,
            IFriendsService friendsService,
            IProfileRepository profileRepository,
            ViewDependencies viewDependencies) : base(viewFactory)
        {
            this.friendsService = friendsService;
            this.profileRepository = profileRepository;
            this.viewDependencies = viewDependencies;
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

            viewInstance!.InjectDependencies(viewDependencies);
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
                viewInstance!.ProfilePicture.SetLoadingState(true);
                viewInstance!.DescriptionLabel.text = "Are you sure you want to unfriend?";

                Profile? profile = await profileRepository.GetAsync(inputData.UserId, ct);

                if (profile == null) return;

                viewInstance!.DescriptionLabel.text = $"Are you sure you want to unfriend {profile.Name}?";
                viewInstance!.ProfilePicture.Setup(profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, inputData.UserId);
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
                EnumResult<TaskError> result = await friendsService.DeleteFriendshipAsync(inputData.UserId, ct).SuppressToResultAsync(ReportCategory.FRIENDS);

                if (result.Success)
                    Close();
            }
        }

        public struct Params
        {
            public Web3Address UserId { get; set; }
        }
    }
}
