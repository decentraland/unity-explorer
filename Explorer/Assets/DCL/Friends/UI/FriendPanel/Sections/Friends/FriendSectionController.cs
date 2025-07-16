using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.Multiplayer.Connectivity;
using DCL.UI.SharedSpaceManager;
using DCL.VoiceChat;
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
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly string[] getUserPositionBuffer = new string[1];
        private readonly IChatEventBus chatEventBus;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private CancellationTokenSource? jumpToFriendLocationCts;
        private CancellationTokenSource popupCts;
        private UniTaskCompletionSource contextMenuTask = new ();

        public FriendSectionController(FriendsSectionView view,
            FriendListRequestManager requestManager,
            IPassportBridge passportBridge,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            IChatEventBus chatEventBus,
            ISharedSpaceManager sharedSpaceManager,
            bool includeCall,
            IVoiceChatOrchestrator voiceChatOrchestrator) : base(view, requestManager)
        {
            this.passportBridge = passportBridge;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.chatEventBus = chatEventBus;
            this.sharedSpaceManager = sharedSpaceManager;
            this.voiceChatOrchestrator = voiceChatOrchestrator;

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

        private void ContextMenuClicked(FriendProfile friendProfile, Vector2 buttonPosition, FriendListUserView elementView)
        {
            elementView.CanUnHover = false;

            popupCts = popupCts.SafeRestart();
            contextMenuTask?.TrySetResult();

            contextMenuTask = new UniTaskCompletionSource();
            UniTask menuTask = UniTask.WhenAny(panelLifecycleTask.Task, contextMenuTask.Task);

            ViewDependencies.GlobalUIViews.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(friendProfile.Address), buttonPosition, default(Vector2),
                popupCts.Token, menuTask, onHide: () => elementView.CanUnHover = true, anchorPoint: MenuAnchorPoint.TOP_RIGHT).Forget();
        }

        private void JumpInClicked(FriendProfile profile) =>
            FriendListSectionUtilities.JumpToFriendLocation(profile.Address, jumpToFriendLocationCts, getUserPositionBuffer, onlineUsersProvider, realmNavigator);

        protected override void ElementClicked(FriendProfile profile) =>
            FriendListSectionUtilities.OpenProfilePassport(profile, passportBridge);

        private void OnChatButtonClicked(FriendProfile elementViewUserProfile)
        {
            OnOpenConversationAsync(elementViewUserProfile).Forget();
        }

        private async UniTaskVoid OnOpenConversationAsync(FriendProfile profile)
        {
            await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true, true));
            chatEventBus.OpenPrivateConversationUsingUserId(profile.Address);
        }
    }
}
