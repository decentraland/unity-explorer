using Cysharp.Threading.Tasks;

#if !NO_LIVEKIT_MODE
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
#endif

using DCL.Multiplayer.Connectivity;
using DCL.Passport;
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

#if !NO_LIVEKIT_MODE
        private readonly IChatEventBus chatEventBus;
#endif

        private readonly ISharedSpaceManager sharedSpaceManager;

        private CancellationTokenSource? jumpToFriendLocationCts;
        private CancellationTokenSource popupCts;
        private UniTaskCompletionSource contextMenuTask = new ();

        public FriendSectionController(FriendsSectionView view,
            FriendListRequestManager requestManager,
            IPassportBridge passportBridge,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,

#if !NO_LIVEKIT_MODE
            IChatEventBus chatEventBus,
#endif

            ISharedSpaceManager sharedSpaceManager,
            IVoiceChatOrchestrator voiceChatOrchestrator) : base(view, requestManager)
        {
            this.passportBridge = passportBridge;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;

#if !NO_LIVEKIT_MODE
            this.chatEventBus = chatEventBus;
#endif

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
#if !NO_LIVEKIT_MODE
            await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatMainSharedAreaControllerShowParams(true, true));
            chatEventBus.OpenPrivateConversationUsingUserId(profile.Address);
#else
            Debug.LogError("Conversations are not supported without livekit");
#endif
        }
    }
}
