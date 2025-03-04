using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Multiplayer.Connectivity;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Web3;
using ECS.SceneLifeCycle.Realm;
using MVC;
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
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly string[] getUserPositionBuffer = new string[1];
        private readonly GenericContextMenu contextMenu;

        private CancellationTokenSource? jumpToFriendLocationCts;
        private FriendProfile contextMenuFriendProfile;

        public FriendSectionController(FriendsSectionView view,
            IMVCManager mvcManager,
            ISystemClipboard systemClipboard,
            FriendListRequestManager requestManager,
            IPassportBridge passportBridge,
            IProfileThumbnailCache profileThumbnailCache,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            bool includeUserBlocking) : base(view, requestManager)
        {
            this.mvcManager = mvcManager;
            this.passportBridge = passportBridge;
            this.profileThumbnailCache = profileThumbnailCache;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;

            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, HandleContextMenuUserProfileButton);

            contextMenu = FriendListSectionUtilities.BuildContextMenu(view.ContextMenuSettings,
                userProfileContextMenuControlSettings, includeUserBlocking, OpenProfilePassportCtx, null, BlockUserCtx).Item1;

            requestManager.ContextMenuClicked += ContextMenuClicked;
            requestManager.JumpInClicked += JumpInClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            requestManager.JumpInClicked -= JumpInClicked;
            jumpToFriendLocationCts.SafeCancelAndDispose();
        }

        private void OpenProfilePassportCtx() =>
            FriendListSectionUtilities.OpenProfilePassport(contextMenuFriendProfile, passportBridge);

        private void BlockUserCtx() =>
            FriendListSectionUtilities.BlockUserClicked(contextMenuFriendProfile);

        private void HandleContextMenuUserProfileButton(string userId, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            mvcManager.ShowAsync(UnfriendConfirmationPopupController.IssueCommand(new UnfriendConfirmationPopupController.Params
            {
                UserId = new Web3Address(userId),
            })).Forget();
        }

        private void ContextMenuClicked(FriendProfile friendProfile, Vector2 buttonPosition, FriendListUserView elementView)
        {
            contextMenuFriendProfile = friendProfile;

            userProfileContextMenuControlSettings.SetInitialData(friendProfile.Name, friendProfile.Address, friendProfile.HasClaimedName,
                friendProfile.UserNameColor, UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND,
                profileThumbnailCache.GetThumbnail(friendProfile.Address.ToString()));

            elementView.CanUnHover = false;

            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                           new GenericContextMenuParameter(
                               config: contextMenu,
                               anchorPosition: buttonPosition,
                               actionOnHide: () => elementView.CanUnHover = true,
                               closeTask: panelLifecycleTask?.Task))
                       )
                      .Forget();
        }

        private void JumpInClicked(FriendProfile profile) =>
            FriendListSectionUtilities.JumpToFriendLocation(profile.Address, jumpToFriendLocationCts, getUserPositionBuffer, onlineUsersProvider, realmNavigator);

        protected override void ElementClicked(FriendProfile profile) =>
            FriendListSectionUtilities.OpenProfilePassport(profile, passportBridge);
    }
}
