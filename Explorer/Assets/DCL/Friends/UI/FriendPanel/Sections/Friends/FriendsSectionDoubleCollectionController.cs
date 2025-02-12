using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Multiplayer.Connectivity;
using DCL.RealmNavigation;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public class FriendsSectionDoubleCollectionController : FriendPanelSectionDoubleCollectionController<FriendsSectionView, FriendListPagedDoubleCollectionRequestManager, FriendListUserView>
    {
        private readonly IPassportBridge passportBridge;
        private readonly IProfileThumbnailCache profileThumbnailCache;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly IFriendOnlineStatusCache friendOnlineStatusCache;
        private readonly ILoadingStatus loadingStatus;
        private readonly bool includeUserBlocking;
        private readonly string[] getUserPositionBuffer = new string[1];

        private CancellationTokenSource jumpToFriendLocationCts = new ();
        private CancellationTokenSource? prewarmFriendsCts;

        public FriendsSectionDoubleCollectionController(FriendsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            FriendListPagedDoubleCollectionRequestManager doubleCollectionRequestManager,
            IPassportBridge passportBridge,
            IProfileThumbnailCache profileThumbnailCache,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            ISystemClipboard systemClipboard,
            IFriendOnlineStatusCache friendOnlineStatusCache,
            ILoadingStatus loadingStatus,
            bool includeUserBlocking)
            : base(view, friendsService, friendEventBus, web3IdentityCache, mvcManager, doubleCollectionRequestManager)
        {
            this.profileThumbnailCache = profileThumbnailCache;
            this.passportBridge = passportBridge;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.friendOnlineStatusCache = friendOnlineStatusCache;
            this.loadingStatus = loadingStatus;
            this.includeUserBlocking = includeUserBlocking;

            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, HandleContextMenuUserProfileButton);

            doubleCollectionRequestManager.JumpInClicked += JumpInClicked;
            doubleCollectionRequestManager.ContextMenuClicked += ContextMenuClicked;

            // TODO: we need friends to fetch connectivity status for the current middle-term solution. Remove it after the server stream works as expected
            loadingStatus.CurrentStage.Subscribe(PrewarmFriends);
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            requestManager.JumpInClicked -= JumpInClicked;
            jumpToFriendLocationCts.SafeCancelAndDispose();
            prewarmFriendsCts.SafeCancelAndDispose();
        }

        private void HandleContextMenuUserProfileButton(string userId, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            mvcManager.ShowAsync(UnfriendConfirmationPopupController.IssueCommand(new UnfriendConfirmationPopupController.Params
            {
                UserId = new Web3Address(userId),
            })).Forget();
        }

        protected override void ElementClicked(FriendProfile profile)
        {
            passportBridge.ShowAsync(profile.Address).Forget();
        }

        private void ContextMenuClicked(FriendProfile friendProfile, Vector2 buttonPosition, FriendListUserView elementView)
        {
            jumpToFriendLocationCts = jumpToFriendLocationCts.SafeRestart();

            userProfileContextMenuControlSettings.SetInitialData(friendProfile.Name, friendProfile.Address, friendProfile.HasClaimedName,
                view.ChatEntryConfiguration.GetNameColor(friendProfile.Name), UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND,
                profileThumbnailCache.GetThumbnail(friendProfile.Address.ToString()));

            elementView.CanUnHover = false;

            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                           new GenericContextMenuParameter(
                               config: FriendListSectionUtilities.BuildContextMenu(friendProfile, view.ContextMenuSettings,
                                   userProfileContextMenuControlSettings, onlineUsersProvider, realmNavigator, passportBridge,
                                   getUserPositionBuffer, jumpToFriendLocationCts, includeUserBlocking, friendOnlineStatusCache.GetFriendStatus(friendProfile.Address) != OnlineStatus.OFFLINE),
                               anchorPosition: buttonPosition,
                               actionOnHide: () => elementView.CanUnHover = true,
                               closeTask: panelLifecycleTask?.Task))
                       )
                      .Forget();
        }

        private void JumpInClicked(FriendProfile profile)
        {
            jumpToFriendLocationCts = jumpToFriendLocationCts.SafeRestart();
            FriendListSectionUtilities.JumpToFriendLocation(profile.Address, jumpToFriendLocationCts, getUserPositionBuffer, onlineUsersProvider, realmNavigator);
        }

        private void PrewarmFriends(LoadingStatus.LoadingStage stage)
        {
            if (stage != LoadingStatus.LoadingStage.Completed) return;

            prewarmFriendsCts = prewarmFriendsCts.SafeRestart();
            PrewarmAsync(prewarmFriendsCts.Token).Forget();
            return;

            async UniTaskVoid PrewarmAsync(CancellationToken ct)
            {
                await InitAsync(ct);
                loadingStatus.CurrentStage.Unsubscribe(PrewarmFriends);
            }
        }
    }
}
