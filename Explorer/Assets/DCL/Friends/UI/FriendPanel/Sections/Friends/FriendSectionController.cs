using Cysharp.Threading.Tasks;
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
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly string[] getUserPositionBuffer = new string[1];
        private readonly ViewDependencies viewDependencies;

        private CancellationTokenSource? jumpToFriendLocationCts;
        private FriendProfile contextMenuFriendProfile;
        private CancellationTokenSource popupCts;
        private UniTaskCompletionSource contextMenuTask = new ();

        public FriendSectionController(FriendsSectionView view,
            IMVCManager mvcManager,
            FriendListRequestManager requestManager,
            IPassportBridge passportBridge,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            bool includeUserBlocking,
            ViewDependencies viewDependencies) : base(view, requestManager)
        {
            this.mvcManager = mvcManager;
            this.passportBridge = passportBridge;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.viewDependencies = viewDependencies;

            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(HandleContextMenuUserProfileButton);

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

        private void HandleContextMenuUserProfileButton(string userId, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            mvcManager.ShowAsync(UnfriendConfirmationPopupController.IssueCommand(new UnfriendConfirmationPopupController.Params
                       {
                           UserId = new Web3Address(userId),
                       }))
                      .Forget();
        }

        private void ContextMenuClicked(FriendProfile friendProfile, Vector2 buttonPosition, FriendListUserView elementView)
        {
            contextMenuFriendProfile = friendProfile;

            elementView.CanUnHover = false;

            popupCts = popupCts.SafeRestart();
            contextMenuTask?.TrySetResult();

            contextMenuTask = new UniTaskCompletionSource();
            UniTask menuTask = UniTask.WhenAny(panelLifecycleTask.Task, contextMenuTask.Task);
            viewDependencies.GlobalUIViews.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(contextMenuFriendProfile.Address), buttonPosition, default(Vector2), popupCts.Token, menuTask, anchorPoint: MenuAnchorPoint.TOP_RIGHT).Forget();
        }

        private void JumpInClicked(FriendProfile profile) =>
            FriendListSectionUtilities.JumpToFriendLocation(profile.Address, jumpToFriendLocationCts, getUserPositionBuffer, onlineUsersProvider, realmNavigator);

        protected override void ElementClicked(FriendProfile profile) =>
            FriendListSectionUtilities.OpenProfilePassport(profile, passportBridge);
    }
}
