using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connectivity;
using DCL.UI;
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
        private const float DELAY_BETWEEN_CLICKS = 0.5f;

        private readonly IPassportBridge passportBridge;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker;
        private readonly string[] getUserPositionBuffer = new string[1];
        private readonly GenericContextMenu contextMenu;
        private readonly GenericContextMenuElement contextMenuJumpInButton;
        private readonly GenericContextMenuElement contextMenuCallButton;

        private CancellationTokenSource jumpToFriendLocationCts = new ();
        private FriendProfile contextMenuFriendProfile;
        private CancellationTokenSource openPassportCts = new ();
        private bool elementClicked;

        internal event Action<string>? OnlineFriendClicked;
        internal event Action<string, Vector2Int>? JumpInClicked;
        internal event Action<Web3Address> OpenConversationClicked;

        public FriendsSectionDoubleCollectionController(FriendsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IMVCManager mvcManager,
            FriendListPagedDoubleCollectionRequestManager doubleCollectionRequestManager,
            IPassportBridge passportBridge,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker,
            bool includeUserBlocking,
            bool includeCall)
            : base(view, friendsService, friendEventBus, mvcManager, doubleCollectionRequestManager)
        {
            this.passportBridge = passportBridge;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.friendsConnectivityStatusTracker = friendsConnectivityStatusTracker;

            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(HandleContextMenuUserProfileButton);

            var buildContextMenu = FriendListSectionUtilities.BuildContextMenu(view.ContextMenuSettings,
                userProfileContextMenuControlSettings, includeUserBlocking, includeCall, OpenProfilePassportCtx, JumpToFriendLocationCtx, CallFriendCtx, BlockUserCtx);

            contextMenu = buildContextMenu.Item1;
            contextMenuJumpInButton = buildContextMenu.Item2;
            contextMenuCallButton = buildContextMenu.Item3;

            doubleCollectionRequestManager.JumpInClicked += JumpInClick;
            doubleCollectionRequestManager.ContextMenuClicked += ContextMenuClicked;
            doubleCollectionRequestManager.NoFriendsInCollections += ShowEmptyState;
            doubleCollectionRequestManager.AtLeastOneFriendInCollections += HideEmptyState;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            requestManager.JumpInClicked -= JumpInClick;
            requestManager.NoFriendsInCollections -= ShowEmptyState;
            requestManager.AtLeastOneFriendInCollections -= HideEmptyState;
            jumpToFriendLocationCts.SafeCancelAndDispose();
        }

        private void JumpToFriendLocationCtx() =>
            FriendListSectionUtilities.JumpToFriendLocation(contextMenuFriendProfile.Address, jumpToFriendLocationCts, getUserPositionBuffer, onlineUsersProvider, realmNavigator, parcel => JumpInClicked?.Invoke(contextMenuFriendProfile.Address, parcel));

        private void OpenProfilePassportCtx() =>
            FriendListSectionUtilities.OpenProfilePassport(contextMenuFriendProfile, passportBridge);

        private void CallFriendCtx() =>
            FriendListSectionUtilities.CallFriend(contextMenuFriendProfile.Address, contextMenuFriendProfile.Name);

        private void BlockUserCtx() =>
            FriendListSectionUtilities.BlockUserClicked(mvcManager, contextMenuFriendProfile.Address, contextMenuFriendProfile.Name);

        private void ShowEmptyState()
        {
            view.SetEmptyState(true);
            view.SetScrollViewState(false);
        }

        private void HideEmptyState()
        {
            view.SetEmptyState(false);
            view.SetScrollViewState(true);
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
            if (elementClicked)
            {
                OpenConversationClicked?.Invoke(profile.Address);
                openPassportCts.Cancel();
                elementClicked = false;
            }
            else
            {
                openPassportCts = openPassportCts.SafeRestart();
                WaitAndOpenPassportAsync(profile, openPassportCts.Token).Forget();
            }

        }

        private async UniTaskVoid WaitAndOpenPassportAsync(FriendProfile profile, CancellationToken ct)
        {
            elementClicked = true;
            if (friendsConnectivityStatusTracker.GetFriendStatus(profile.Address) != OnlineStatus.OFFLINE)
                OnlineFriendClicked?.Invoke(profile.Address);

            await UniTask.Delay(TimeSpan.FromSeconds(DELAY_BETWEEN_CLICKS), cancellationToken: ct);
            elementClicked = false;


            await passportBridge.ShowAsync(profile.Address);
        }



        private void ContextMenuClicked(FriendProfile friendProfile, Vector2 buttonPosition, FriendListUserView elementView)
        {
            jumpToFriendLocationCts = jumpToFriendLocationCts.SafeRestart();
            contextMenuFriendProfile = friendProfile;

            userProfileContextMenuControlSettings.SetInitialData(friendProfile.Name, friendProfile.Address, friendProfile.HasClaimedName,
                friendProfile.UserNameColor, UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND,
                friendProfile.FacePictureUrl);

            elementView.CanUnHover = false;

            bool isFriendOnline = friendsConnectivityStatusTracker.GetFriendStatus(friendProfile.Address) != OnlineStatus.OFFLINE;

            contextMenuJumpInButton.Enabled = isFriendOnline;
            contextMenuCallButton.Enabled = isFriendOnline;

            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                           new GenericContextMenuParameter(
                               config: contextMenu,
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
