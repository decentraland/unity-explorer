using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.PushNotifications
{
    public class FriendPushNotificationController : ControllerBase<FriendPushNotificationView>
    {
        private readonly IFriendsEventBus friendEventBus;
        private readonly IProfileThumbnailCache profileThumbnailCache;
        private readonly bool isConnectivityStatusEnabled;

        private CancellationTokenSource toastAnimationCancellationTokenSource = new ();

        public FriendPushNotificationController(ViewFactoryMethod viewFactory,
            IFriendsEventBus friendEventBus,
            IProfileThumbnailCache profileThumbnailCache,
            bool isConnectivityStatusEnabled) : base(viewFactory)
        {
            this.friendEventBus = friendEventBus;
            this.profileThumbnailCache = profileThumbnailCache;
            this.isConnectivityStatusEnabled = isConnectivityStatusEnabled;
        }

        public override void Dispose()
        {
            base.Dispose();
            friendEventBus.OnFriendConnected -= FriendConnected;
            toastAnimationCancellationTokenSource.SafeCancelAndDispose();
        }

        protected override void OnViewInstantiated()
        {
            if (isConnectivityStatusEnabled)
                friendEventBus.OnFriendConnected += FriendConnected;
        }

        private void FriendConnected(FriendProfile friendProfile)
        {
            toastAnimationCancellationTokenSource = toastAnimationCancellationTokenSource.SafeRestart();
            ResolveThumbnailAndShowAsync(toastAnimationCancellationTokenSource.Token).Forget();

            async UniTaskVoid ResolveThumbnailAndShowAsync(CancellationToken ct)
            {
                viewInstance!.HideToast();

                Sprite? profileThumbnail = await profileThumbnailCache.GetThumbnailAsync(friendProfile.Address, friendProfile.FacePictureUrl, ct);

                viewInstance.ConfigureForFriend(friendProfile, profileThumbnail);
                await viewInstance.ShowToastAsync(ct);
            }
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
