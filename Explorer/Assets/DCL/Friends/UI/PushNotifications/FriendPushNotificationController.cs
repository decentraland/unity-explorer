using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.PushNotifications
{
    public class FriendPushNotificationController : ControllerBase<FriendPushNotificationView>
    {
        private readonly IFriendOnlineStatusCache friendOnlineStatusCache;
        private readonly IProfileThumbnailCache profileThumbnailCache;

        private CancellationTokenSource toastAnimationCancellationTokenSource = new ();

        public FriendPushNotificationController(ViewFactoryMethod viewFactory,
            IFriendOnlineStatusCache friendOnlineStatusCache,
            IProfileThumbnailCache profileThumbnailCache) : base(viewFactory)
        {
            this.friendOnlineStatusCache = friendOnlineStatusCache;
            this.profileThumbnailCache = profileThumbnailCache;

            friendOnlineStatusCache.OnFriendBecameOnline += FriendConnected;
        }

        public override void Dispose()
        {
            base.Dispose();
            friendOnlineStatusCache.OnFriendBecameOnline -= FriendConnected;
            toastAnimationCancellationTokenSource.SafeCancelAndDispose();
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
