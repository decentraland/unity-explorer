using Cysharp.Threading.Tasks;
using DCL.UI.Profiles.Helpers;
using DCL.RealmNavigation;
using DCL.Utilities;
using MVC;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.PushNotifications
{
    public class FriendPushNotificationController : ControllerBase<FriendPushNotificationView>
    {
        private const int SUBSCRIPTION_DELAY_MS = 5000;

        private readonly IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker;
        private readonly ILoadingStatus loadingStatus;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

        private CancellationTokenSource toastAnimationCancellationTokenSource = new ();
        private CancellationTokenSource? subscribeCancellationTokenSource;

        public FriendPushNotificationController(ViewFactoryMethod viewFactory,
            IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker,
            ProfileRepositoryWrapper profileDataProvider,
            ILoadingStatus loadingStatus) : base(viewFactory)
        {
            this.friendsConnectivityStatusTracker = friendsConnectivityStatusTracker;
            this.profileRepositoryWrapper = profileDataProvider;
            this.loadingStatus = loadingStatus;

            loadingStatus.CurrentStage.Subscribe(OnLoadingStatusChanged);
        }

        public override void Dispose()
        {
            base.Dispose();
            friendsConnectivityStatusTracker.OnFriendBecameOnline -= FriendConnected;
            toastAnimationCancellationTokenSource.SafeCancelAndDispose();
        }

        private void OnLoadingStatusChanged(LoadingStatus.LoadingStage stage)
        {
            if (stage != LoadingStatus.LoadingStage.Completed) return;

            subscribeCancellationTokenSource = subscribeCancellationTokenSource.SafeRestart();
            WaitAndSubscribeToOnlineChangesAsync(subscribeCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid WaitAndSubscribeToOnlineChangesAsync(CancellationToken ct)
            {
                // Insert a fake delay so we skip all the initial connectivity updates from friends that are already online
                await UniTask.Delay(SUBSCRIPTION_DELAY_MS, cancellationToken: ct);
                friendsConnectivityStatusTracker.OnFriendBecameOnline += FriendConnected;
                loadingStatus.CurrentStage.Unsubscribe(OnLoadingStatusChanged);
            }
        }

        private void FriendConnected(FriendProfile friendProfile)
        {
            toastAnimationCancellationTokenSource = toastAnimationCancellationTokenSource.SafeRestart();
            ResolveThumbnailAndShowAsync(toastAnimationCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid ResolveThumbnailAndShowAsync(CancellationToken ct)
            {
                if (viewInstance == null) return;

                viewInstance.HideToast();
                viewInstance.ConfigureForFriend(friendProfile, profileRepositoryWrapper);

                await viewInstance.ShowToastAsync(ct);
            }
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
