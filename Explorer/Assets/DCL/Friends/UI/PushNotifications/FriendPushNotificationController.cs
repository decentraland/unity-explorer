using Cysharp.Threading.Tasks;
using DCL.UI.Profiles.Helpers;
using DCL.RealmNavigation;
using DCL.UI.EphemeralNotifications;
using DCL.Utilities;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.PushNotifications
{
    public class FriendPushNotificationController
    {
        private const int SUBSCRIPTION_DELAY_MS = 5000;

        private readonly FriendsConnectivityStatusTracker friendsConnectivityStatusTracker;
        private readonly ILoadingStatus loadingStatus;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly EphemeralNotificationsController ephemeralNotificationsController;

        private CancellationTokenSource toastAnimationCancellationTokenSource = new ();
        private CancellationTokenSource? subscribeCancellationTokenSource;

        public FriendPushNotificationController(
            FriendsConnectivityStatusTracker friendsConnectivityStatusTracker,
            ProfileRepositoryWrapper profileDataProvider,
            ILoadingStatus loadingStatus,
            EphemeralNotificationsController ephemeralNotificationsController)
        {
            this.friendsConnectivityStatusTracker = friendsConnectivityStatusTracker;
            this.profileRepositoryWrapper = profileDataProvider;
            this.loadingStatus = loadingStatus;
            this.ephemeralNotificationsController = ephemeralNotificationsController;

            loadingStatus.CurrentStage.Subscribe(OnLoadingStatusChanged);
        }

        public void Dispose()
        {
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
                await profileRepositoryWrapper.GetProfileThumbnailAsync(friendProfile.FacePictureUrl, ct);

                ephemeralNotificationsController.AddNotificationAsync("FriendOnlineEphemeralNotification", friendProfile.Address, null).Forget();
            }
        }
    }
}
