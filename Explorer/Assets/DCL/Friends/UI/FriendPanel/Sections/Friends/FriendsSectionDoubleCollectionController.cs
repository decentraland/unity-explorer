using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.Multiplayer.Connectivity;
using DCL.UI;
using DCL.UI.SharedSpaceManager;
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
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly FriendsConnectivityStatusTracker friendsConnectivityStatusTracker;
        private readonly string[] getUserPositionBuffer = new string[1];
        private readonly ViewDependencies viewDependencies;
        private readonly IChatEventBus chatEventBus;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private CancellationTokenSource jumpToFriendLocationCts = new ();
        private CancellationTokenSource openPassportCts = new ();
        private bool elementClicked;
        private CancellationTokenSource? popupCts;
        private UniTaskCompletionSource contextMenuTask = new ();

        internal event Action<string>? OnlineFriendClicked;
        internal event Action<string, Vector2Int>? JumpInClicked;
        internal event Action<Web3Address>? OpenConversationClicked;

        public FriendsSectionDoubleCollectionController(FriendsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IMVCManager mvcManager,
            FriendListPagedDoubleCollectionRequestManager doubleCollectionRequestManager,
            IPassportBridge passportBridge,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            FriendsConnectivityStatusTracker friendsConnectivityStatusTracker,
            IChatEventBus chatEventBus,
            ISharedSpaceManager sharedSpaceManager,
            ViewDependencies viewDependencies)
            : base(view, friendsService, friendEventBus, mvcManager, doubleCollectionRequestManager)
        {
            this.passportBridge = passportBridge;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.friendsConnectivityStatusTracker = friendsConnectivityStatusTracker;
            this.chatEventBus = chatEventBus;
            this.sharedSpaceManager = sharedSpaceManager;
            this.viewDependencies = viewDependencies;

            doubleCollectionRequestManager.JumpInClicked += OnJumpInClicked;
            doubleCollectionRequestManager.ContextMenuClicked += OnContextMenuClicked;
            doubleCollectionRequestManager.ChatClicked += OnChatButtonClicked;
            doubleCollectionRequestManager.NoFriendsInCollections += ShowEmptyState;
            doubleCollectionRequestManager.AtLeastOneFriendInCollections += HideEmptyState;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ContextMenuClicked -= OnContextMenuClicked;
            requestManager.JumpInClicked -= OnJumpInClicked;
            requestManager.ChatClicked -= OnChatButtonClicked;
            requestManager.NoFriendsInCollections -= ShowEmptyState;
            requestManager.AtLeastOneFriendInCollections -= HideEmptyState;
            jumpToFriendLocationCts.SafeCancelAndDispose();
        }

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

        private void OnContextMenuClicked(FriendProfile friendProfile, Vector2 buttonPosition, FriendListUserView elementView)
        {
            jumpToFriendLocationCts = jumpToFriendLocationCts.SafeRestart();
            elementView.CanUnHover = false;

            bool isFriendOnline = friendsConnectivityStatusTracker.GetFriendStatus(friendProfile.Address) != OnlineStatus.OFFLINE;

            if (isFriendOnline)
                OnlineFriendClicked?.Invoke(friendProfile.Address);

            popupCts = popupCts.SafeRestart();
            contextMenuTask.TrySetResult();

            contextMenuTask = new UniTaskCompletionSource();
            UniTask menuTask = UniTask.WhenAny(panelLifecycleTask.Task, contextMenuTask.Task);

            viewDependencies.GlobalUIViews.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(friendProfile.Address),
                buttonPosition, default(Vector2), popupCts.Token, closeMenuTask: menuTask, onHide: () => elementView.CanUnHover = true
                ,anchorPoint: MenuAnchorPoint.TOP_RIGHT).Forget();
        }

        private void OnJumpInClicked(FriendProfile profile)
        {
            jumpToFriendLocationCts = jumpToFriendLocationCts.SafeRestart();
            FriendListSectionUtilities.JumpToFriendLocation(profile.Address, jumpToFriendLocationCts, getUserPositionBuffer, onlineUsersProvider, realmNavigator, parcel => JumpInClicked?.Invoke(profile.Address, parcel));
        }

        private void OnChatButtonClicked(FriendProfile elementViewUserProfile)
        {
            OnOpenConversationAsync(elementViewUserProfile).Forget();
        }

        private async UniTaskVoid OnOpenConversationAsync(FriendProfile profile)
        {
            await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true, true));
            chatEventBus.OpenConversationUsingUserId(profile.Address);
        }
    }
}
