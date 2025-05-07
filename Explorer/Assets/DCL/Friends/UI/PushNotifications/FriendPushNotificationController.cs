using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.PushNotifications
{
    public class FriendPushNotificationController : ControllerBase<FriendPushNotificationView>
    {
        private readonly IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker;
        private readonly ViewDependencies viewDependencies;

        private CancellationTokenSource toastAnimationCancellationTokenSource = new ();

        public FriendPushNotificationController(ViewFactoryMethod viewFactory,
            IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker,
            ViewDependencies viewDependencies) : base(viewFactory)
        {
            this.friendsConnectivityStatusTracker = friendsConnectivityStatusTracker;
            this.viewDependencies = viewDependencies;

            friendsConnectivityStatusTracker.OnFriendBecameOnline += FriendConnected;
        }

        public override void Dispose()
        {
            base.Dispose();
            friendsConnectivityStatusTracker.OnFriendBecameOnline -= FriendConnected;
            toastAnimationCancellationTokenSource.SafeCancelAndDispose();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.InjectDependencies(viewDependencies);
        }

        private void FriendConnected(FriendProfile friendProfile)
        {
            toastAnimationCancellationTokenSource = toastAnimationCancellationTokenSource.SafeRestart();
            ResolveThumbnailAndShowAsync(toastAnimationCancellationTokenSource.Token).Forget();

            async UniTaskVoid ResolveThumbnailAndShowAsync(CancellationToken ct)
            {
                if (viewInstance == null) return;

                viewInstance.HideToast();
                viewInstance.ConfigureForFriend(friendProfile);
                await viewInstance.ShowToastAsync(ct);
            }
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
