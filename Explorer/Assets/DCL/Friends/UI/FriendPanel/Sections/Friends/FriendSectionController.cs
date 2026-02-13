using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Multiplayer.Connectivity;
using DCL.Passport;
using DCL.Profiles;
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
        private readonly IPassportBridge passportBridge;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly string[] getUserPositionBuffer = new string[1];

        private CancellationTokenSource? jumpToFriendLocationCts;
        private CancellationTokenSource popupCts = new ();
        private UniTaskCompletionSource contextMenuTask = new ();

        public FriendSectionController(FriendsSectionView view,
            FriendListRequestManager requestManager,
            IPassportBridge passportBridge,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator) : base(view, requestManager)
        {
            this.passportBridge = passportBridge;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;

            requestManager.ContextMenuClicked += ContextMenuClicked;
            requestManager.JumpInClicked += JumpInClicked;
            requestManager.ChatClicked += OnChatButtonClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            requestManager.JumpInClicked -= JumpInClicked;
            requestManager.ChatClicked -= OnChatButtonClicked;
            jumpToFriendLocationCts.SafeCancelAndDispose();
        }

        private void ContextMenuClicked(Profile.CompactInfo friendProfile, Vector2 buttonPosition, FriendListUserView elementView)
        {
            elementView.CanUnHover = false;

            popupCts = popupCts.SafeRestart();
            contextMenuTask.TrySetResult();

            contextMenuTask = new UniTaskCompletionSource();
            UniTask menuTask = UniTask.WhenAny(panelLifecycleTask!.Task, contextMenuTask.Task);

            ViewDependencies.GlobalUIViews.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(friendProfile.UserId), buttonPosition, default(Vector2),
                popupCts.Token, menuTask, onHide: () => elementView.CanUnHover = true, anchorPoint: MenuAnchorPoint.TOP_RIGHT).Forget();
        }

        private void JumpInClicked(Profile.CompactInfo profile) =>
            FriendListSectionUtilities.JumpToFriendLocation(profile.Address, jumpToFriendLocationCts, getUserPositionBuffer, onlineUsersProvider, realmNavigator);

        protected override void ElementClicked(Profile.CompactInfo profile) =>
            FriendListSectionUtilities.OpenProfilePassport(profile, passportBridge);

        private void OnChatButtonClicked(Profile.CompactInfo elementViewUserProfile) =>
            ChatOpener.Instance.OpenPrivateConversationWithUserId(elementViewUserProfile.Address);
    }
}
