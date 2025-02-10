using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Multiplayer.Connectivity;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
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
        private readonly IMVCManager mvcManager;
        private readonly IPassportBridge passportBridge;
        private readonly IProfileThumbnailCache profileThumbnailCache;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly bool includeUserBlocking;
        private readonly string[] getUserPositionBuffer = new string[1];

        private CancellationTokenSource? jumpToFriendLocationCts;

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
            bool includeUserBlocking)
            : base(view, friendsService, friendEventBus, web3IdentityCache, mvcManager, doubleCollectionRequestManager)
        {
            this.mvcManager = mvcManager;
            this.profileThumbnailCache = profileThumbnailCache;
            this.passportBridge = passportBridge;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.includeUserBlocking = includeUserBlocking;

            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, HandleContextMenuUserProfileButton);

            doubleCollectionRequestManager.JumpInClicked += JumpInClicked;
            doubleCollectionRequestManager.ContextMenuClicked += ContextMenuClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            requestManager.JumpInClicked -= JumpInClicked;
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
                                   getUserPositionBuffer, jumpToFriendLocationCts, includeUserBlocking, requestManager.IsFriendInGame(friendProfile)),
                               anchorPosition: buttonPosition,
                               actionOnHide: () => elementView.CanUnHover = true,
                               closeTask: panelLifecycleTask?.Task))
                       )
                      .Forget();
        }

        private void JumpInClicked(FriendProfile profile) =>
            FriendListSectionUtilities.JumpToFriendLocation(profile, jumpToFriendLocationCts, getUserPositionBuffer, onlineUsersProvider, realmNavigator);
    }
}
