using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Multiplayer.Connectivity;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Web3;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
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
        private readonly IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker;
        private readonly bool includeUserBlocking;
        private readonly string[] getUserPositionBuffer = new string[1];

        private CancellationTokenSource jumpToFriendLocationCts = new ();

        internal event Action<string>? OnlineFriendClicked;
        internal event Action<string, Vector2Int>? JumpInClicked;

        public FriendsSectionDoubleCollectionController(FriendsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IMVCManager mvcManager,
            FriendListPagedDoubleCollectionRequestManager doubleCollectionRequestManager,
            IPassportBridge passportBridge,
            IProfileThumbnailCache profileThumbnailCache,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            ISystemClipboard systemClipboard,
            IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker,
            bool includeUserBlocking)
            : base(view, friendsService, friendEventBus, mvcManager, doubleCollectionRequestManager)
        {
            this.profileThumbnailCache = profileThumbnailCache;
            this.passportBridge = passportBridge;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.friendsConnectivityStatusTracker = friendsConnectivityStatusTracker;
            this.includeUserBlocking = includeUserBlocking;

            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, HandleContextMenuUserProfileButton);

            doubleCollectionRequestManager.JumpInClicked += JumpInClick;
            doubleCollectionRequestManager.ContextMenuClicked += ContextMenuClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            requestManager.JumpInClicked -= JumpInClick;
            jumpToFriendLocationCts.SafeCancelAndDispose();
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

            if (friendsConnectivityStatusTracker.GetFriendStatus(profile.Address) != OnlineStatus.OFFLINE)
                OnlineFriendClicked?.Invoke(profile.Address);
        }

        private void ContextMenuClicked(FriendProfile friendProfile, Vector2 buttonPosition, FriendListUserView elementView)
        {
            jumpToFriendLocationCts = jumpToFriendLocationCts.SafeRestart();

            userProfileContextMenuControlSettings.SetInitialData(friendProfile.Name, friendProfile.Address, friendProfile.HasClaimedName,
                view.ChatEntryConfiguration.GetNameColor(friendProfile.Name), UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND,
                profileThumbnailCache.GetThumbnail(friendProfile.Address.ToString()));

            elementView.CanUnHover = false;

            bool isFriendOnline = friendsConnectivityStatusTracker.GetFriendStatus(friendProfile.Address) != OnlineStatus.OFFLINE;

            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                           new GenericContextMenuParameter(
                               config: FriendListSectionUtilities.BuildContextMenu(friendProfile, view.ContextMenuSettings,
                                   userProfileContextMenuControlSettings, onlineUsersProvider, realmNavigator, passportBridge,
                                   getUserPositionBuffer, jumpToFriendLocationCts, includeUserBlocking, isFriendOnline, parcel => JumpInClicked?.Invoke(friendProfile.Address, parcel)),
                               anchorPosition: buttonPosition,
                               actionOnHide: () => elementView.CanUnHover = true,
                               closeTask: panelLifecycleTask?.Task))
                       )
                      .Forget();

            if (isFriendOnline)
                OnlineFriendClicked?.Invoke(friendProfile.Address);
        }

        private void JumpInClick(FriendProfile profile)
        {
            jumpToFriendLocationCts = jumpToFriendLocationCts.SafeRestart();
            FriendListSectionUtilities.JumpToFriendLocation(profile.Address, jumpToFriendLocationCts, getUserPositionBuffer, onlineUsersProvider, realmNavigator, parcel => JumpInClicked?.Invoke(profile.Address, parcel));
        }
    }
}
