using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Multiplayer.Connectivity;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public class FriendSectionController : FriendPanelSectionController<FriendsSectionView, FriendListRequestManager, FriendListUserView>
    {
        private readonly IMVCManager mvcManager;
        private readonly IPassportBridge passportBridge;
        private readonly IProfileThumbnailCache profileThumbnailCache;
        private readonly IFriendsService friendsService;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly bool includeUserBlocking;
        private readonly string[] getUserPositionBuffer = new string[1];

        private CancellationTokenSource? friendshipOperationCts;
        private CancellationTokenSource? jumpToFriendLocationCts;

        public FriendSectionController(FriendsSectionView view,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            ISystemClipboard systemClipboard,
            FriendListRequestManager requestManager,
            IPassportBridge passportBridge,
            IProfileThumbnailCache profileThumbnailCache,
            IFriendsService friendsService,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            bool includeUserBlocking) : base(view, web3IdentityCache, requestManager)
        {
            this.mvcManager = mvcManager;
            this.passportBridge = passportBridge;
            this.profileThumbnailCache = profileThumbnailCache;
            this.friendsService = friendsService;
            this.onlineUsersProvider = onlineUsersProvider;
            this.includeUserBlocking = includeUserBlocking;
            this.realmNavigator = realmNavigator;

            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, HandleContextMenuUserProfileButton);

            requestManager.ContextMenuClicked += ContextMenuClicked;
            requestManager.JumpInClicked += JumpInClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            requestManager.JumpInClicked -= JumpInClicked;
            friendshipOperationCts.SafeCancelAndDispose();
            jumpToFriendLocationCts.SafeCancelAndDispose();
        }

        private void HandleContextMenuUserProfileButton(string userId, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            friendshipOperationCts = friendshipOperationCts.SafeRestart();
            DeleteFriendshipAsync(friendshipOperationCts.Token).Forget();
            return;

            async UniTaskVoid DeleteFriendshipAsync(CancellationToken ct)
            {
                await friendsService.DeleteFriendshipAsync(userId, ct);
            }
        }

        private void ContextMenuClicked(FriendProfile friendProfile, Vector2 buttonPosition, FriendListUserView elementView)
        {
            userProfileContextMenuControlSettings.SetInitialData(friendProfile.Name, friendProfile.Address, friendProfile.HasClaimedName,
                view.ChatEntryConfiguration.GetNameColor(friendProfile.Name), UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND,
                profileThumbnailCache.GetThumbnail(friendProfile.Address.ToString()));

            elementView.CanUnHover = false;

            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                           new GenericContextMenuParameter(
                               config: FriendListSectionUtilities.BuildContextMenu(friendProfile, view.ContextMenuSettings,
                                   userProfileContextMenuControlSettings, onlineUsersProvider, realmNavigator, passportBridge,
                                   getUserPositionBuffer, jumpToFriendLocationCts, includeUserBlocking, false),
                               anchorPosition: buttonPosition,
                               actionOnHide: () => elementView.CanUnHover = true,
                               closeTask: panelLifecycleTask?.Task))
                       )
                      .Forget();
        }

        private void JumpInClicked(FriendProfile profile) =>
            FriendListSectionUtilities.JumpToFriendLocation(profile, jumpToFriendLocationCts, getUserPositionBuffer, onlineUsersProvider, realmNavigator);

        protected override void ElementClicked(FriendProfile profile) =>
            FriendListSectionUtilities.OpenProfilePassport(profile, passportBridge);
    }
}
