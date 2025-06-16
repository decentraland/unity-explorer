using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.Multiplayer.Connectivity;
using DCL.UI.SharedSpaceManager;
using DCL.Web3;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System.Threading;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
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
        private readonly ViewDependencies viewDependencies;
        private readonly IChatEventBus chatEventBus;
        private readonly IChatMessagesBus chatMessageBus;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private CancellationTokenSource? jumpToFriendLocationCts;
        private CancellationTokenSource popupCts;
        private UniTaskCompletionSource contextMenuTask = new ();

        public FriendSectionController(FriendsSectionView view,
            FriendListRequestManager requestManager,
            IPassportBridge passportBridge,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            ViewDependencies viewDependencies,
            IChatEventBus chatEventBus,
            IChatMessagesBus chatMessagesBus,
            ISharedSpaceManager sharedSpaceManager) : base(view, requestManager)
        {
            this.passportBridge = passportBridge;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.viewDependencies = viewDependencies;
            this.chatEventBus = chatEventBus;
            this.chatMessageBus = chatMessagesBus;
            this.sharedSpaceManager = sharedSpaceManager;

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

            viewDependencies.GlobalUIViews.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(friendProfile.Address), buttonPosition, default(Vector2),
                popupCts.Token, menuTask, onHide: () => elementView.CanUnHover = true, anchorPoint: MenuAnchorPoint.TOP_RIGHT).Forget();
        }
        
        private async UniTaskVoid HandleJump(FriendProfile profile)
        {
            chatMessageBus.Send(ChatChannel.NEARBY_CHANNEL,
                $"/{ChatCommandsUtils.COMMAND_GOTO} 100,10",
                "passport-jump"
            );
            
            
            // (bool success, bool isInWorld, string parameters, var parcel) =
            //     await FriendListSectionUtilities
            //         .PrepareTeleportTargetAsync(profile.Address,
            //             onlineUsersProvider,
            //             jumpToFriendLocationCts);
            //
            // if(!success) return;
            //
            // chatMessageBus.Send(ChatChannel.NEARBY_CHANNEL,
            //     $"/{ChatCommandsUtils.COMMAND_GOTO} {parameters}",
            //     "passport-jump"
            // );
            //
            // sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat,
            //     new ChatControllerShowParams(true, true)).Forget();
        }

        private void JumpInClicked(FriendProfile profile)
        {
            HandleJump(profile).Forget();
        }

        protected override void ElementClicked(FriendProfile profile) =>
            FriendListSectionUtilities.OpenProfilePassport(profile, passportBridge);

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
