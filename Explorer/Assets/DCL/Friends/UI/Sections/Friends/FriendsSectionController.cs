using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.Sections.Friends
{
    public class FriendsSectionController : IDisposable
    {
        private const int FRIENDS_PAGE_SIZE = 20;

        private readonly FriendsSectionView view;
        private readonly IFriendsService friendsService;
        private readonly IFriendsEventBus friendEventBus;
        private readonly FriendListPagedRequestManager friendListPagedRequestManager;

        private CancellationTokenSource friendListInitCts = new ();

        public FriendsSectionController(FriendsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus)
        {
            this.view = view;
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;

            this.view.Enable += Enable;
            this.view.Disable += Disable;
            friendListPagedRequestManager = new FriendListPagedRequestManager(friendsService, friendEventBus, FRIENDS_PAGE_SIZE);
        }

        public void Dispose()
        {
            view.Enable -= Enable;
            view.Disable -= Disable;
            friendListPagedRequestManager.Dispose();
            friendListInitCts.SafeCancelAndDispose();
        }

        private void Enable()
        {
            if (!friendListPagedRequestManager.WasInitialised)
                Init(friendListInitCts.Token).Forget();
        }

        private async UniTaskVoid Init(CancellationToken ct)
        {
            view.SetLoadingState(true);

            friendListInitCts = friendListInitCts.SafeRestart();
            await friendListPagedRequestManager.Init(ct);

            view.SetEmptyState(!friendListPagedRequestManager.HasFriends);
            view.SetLoadingState(false);
        }

        private void Disable()
        {
            friendListInitCts = friendListInitCts.SafeRestart();
        }
    }
}
