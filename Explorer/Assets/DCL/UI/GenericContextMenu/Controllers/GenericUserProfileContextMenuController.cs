using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UI;
using DCL.Friends.UI.BlockUserPrompt;
using DCL.Friends.UI.FriendPanel.Sections.Friends;
using DCL.Friends.UI.Requests;
using DCL.Multiplayer.Connectivity;
using DCL.Passport;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.Web3;
using ECS.SceneLifeCycle.Realm;
using MVC;
using Segment.Serialization;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI.GenericContextMenu.Controllers
{
    public class GenericUserProfileContextMenuController
    {
        private delegate void StringDelegate(string id);

        private const int CONTEXT_MENU_SEPARATOR_HEIGHT = 20;
        private const int CONTEXT_MENU_ELEMENTS_SPACING = 5;
        private const int CONTEXT_MENU_WIDTH = 250;
        private static readonly RectOffset CONTEXT_MENU_VERTICAL_LAYOUT_PADDING = new (15, 15, 20, 25);
        private static readonly Vector2 CONTEXT_MENU_OFFSET = new (5, -10);

        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly ObjectProxy<IFriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy;
        private readonly IMVCManager mvcManager;
        private readonly IChatEventBus chatEventBus;
        private readonly bool includeUserBlocking;
        private readonly IAnalyticsController analytics;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;

        private readonly string[] getUserPositionBuffer = new string[1];

        private readonly Controls.Configs.GenericContextMenu contextMenu;
        private readonly UserProfileContextMenuControlSettings userProfileControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> openUserProfileButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> mentionUserButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> jumpInButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> blockButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> openConversationControlSettings;
        private readonly GenericContextMenuElement contextMenuJumpInButton;
        private readonly GenericContextMenuElement contextMenuBlockUserButton;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private CancellationTokenSource cancellationTokenSource;
        private UniTaskCompletionSource closeContextMenuTask;
        private Profile targetProfile;

        public GenericUserProfileContextMenuController(
            ObjectProxy<IFriendsService> friendServiceProxy,
            IChatEventBus chatEventBus,
            IMVCManager mvcManager,
            GenericUserProfileContextMenuSettings contextMenuSettings,
            IAnalyticsController analytics,
            bool includeUserBlocking,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            ObjectProxy<IFriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy,
            ISharedSpaceManager sharedSpaceManager)
        {
            this.friendServiceProxy = friendServiceProxy;
            this.chatEventBus = chatEventBus;
            this.mvcManager = mvcManager;
            this.analytics = analytics;
            this.includeUserBlocking = includeUserBlocking;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.friendOnlineStatusCacheProxy = friendOnlineStatusCacheProxy;
            this.sharedSpaceManager = sharedSpaceManager;
            this.includeUserBlocking = includeUserBlocking;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;

            userProfileControlSettings = new UserProfileContextMenuControlSettings(OnFriendsButtonClicked);
            openUserProfileButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.OpenUserProfileButtonConfig.Text, contextMenuSettings.OpenUserProfileButtonConfig.Sprite, new StringDelegate(OnShowUserPassportClicked));
            mentionUserButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.MentionButtonConfig.Text, contextMenuSettings.MentionButtonConfig.Sprite, new StringDelegate(OnMentionUserClicked));
            jumpInButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.JumpInButtonConfig.Text, contextMenuSettings.JumpInButtonConfig.Sprite, new StringDelegate(OnJumpInClicked));
            blockButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.BlockButtonConfig.Text, contextMenuSettings.BlockButtonConfig.Sprite, new StringDelegate(OnBlockUserClicked));
            openConversationControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.OpenConversationButtonConfig.Text, contextMenuSettings.OpenConversationButtonConfig.Sprite, new StringDelegate(OnOpenConversationButtonClicked));

            contextMenuJumpInButton = new GenericContextMenuElement(jumpInButtonControlSettings, false);
            contextMenuBlockUserButton = new GenericContextMenuElement(blockButtonControlSettings, false);

            contextMenu = new Controls.Configs.GenericContextMenu(CONTEXT_MENU_WIDTH, CONTEXT_MENU_OFFSET, CONTEXT_MENU_VERTICAL_LAYOUT_PADDING, CONTEXT_MENU_ELEMENTS_SPACING, anchorPoint: ContextMenuOpenDirection.BOTTOM_RIGHT)
                         .AddControl(userProfileControlSettings)
                         .AddControl(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right))
                         .AddControl(mentionUserButtonControlSettings)
                         .AddControl(openUserProfileButtonControlSettings)
                         .AddControl(openConversationControlSettings)
                         .AddControl(contextMenuJumpInButton)
                         .AddControl(contextMenuBlockUserButton);
        }

        public async UniTask ShowUserProfileContextMenuAsync(Profile profile, Vector3 position, Vector2 offset,
            CancellationToken ct, UniTask closeMenuTask, Action onContextMenuHide = null,
            ContextMenuOpenDirection anchorPoint = ContextMenuOpenDirection.BOTTOM_RIGHT)
        {
            closeContextMenuTask?.TrySetResult();
            closeContextMenuTask = new UniTaskCompletionSource();
            UniTask closeTask = UniTask.WhenAny(closeContextMenuTask.Task, closeMenuTask);
            UserProfileContextMenuControlSettings.FriendshipStatus contextMenuFriendshipStatus = UserProfileContextMenuControlSettings.FriendshipStatus.DISABLED;
            targetProfile = profile;

            if (friendServiceProxy.Configured)
            {
                FriendshipStatus friendshipStatus = await friendServiceProxy.Object.GetFriendshipStatusAsync(profile.UserId, ct);
                contextMenuFriendshipStatus = ConvertFriendshipStatus(friendshipStatus);
                blockButtonControlSettings.SetData(profile.UserId);
                jumpInButtonControlSettings.SetData(profile.UserId);
                contextMenuBlockUserButton.Enabled = includeUserBlocking && friendshipStatus != FriendshipStatus.BLOCKED;

                contextMenuJumpInButton.Enabled = friendshipStatus == FriendshipStatus.FRIEND &&
                                                  friendOnlineStatusCacheProxy.Object.GetFriendStatus(profile.UserId) != OnlineStatus.OFFLINE;
            }

            userProfileControlSettings.SetInitialData(profile.ValidatedName, profile.UserId,
                profile.HasClaimedName, profile.UserNameColor, contextMenuFriendshipStatus, profile.Avatar.FaceSnapshotUrl);

            mentionUserButtonControlSettings.SetData(profile.MentionName);
            openUserProfileButtonControlSettings.SetData(profile.UserId);
            openConversationControlSettings.SetData(profile.UserId);

            contextMenu.ChangeAnchorPoint(anchorPoint);

            if (offset == default(Vector2))
                offset = CONTEXT_MENU_OFFSET;

            contextMenu.ChangeOffsetFromTarget(offset);

            await mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                new GenericContextMenuParameter(contextMenu, position, actionOnHide: onContextMenuHide, closeTask: closeTask)), ct);
        }

        private UserProfileContextMenuControlSettings.FriendshipStatus ConvertFriendshipStatus(FriendshipStatus friendshipStatus)
        {
            return friendshipStatus switch
                   {
                       FriendshipStatus.NONE => UserProfileContextMenuControlSettings.FriendshipStatus.NONE,
                       FriendshipStatus.FRIEND => UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND,
                       FriendshipStatus.REQUEST_SENT => UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT,
                       FriendshipStatus.REQUEST_RECEIVED => UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED,
                       FriendshipStatus.BLOCKED => UserProfileContextMenuControlSettings.FriendshipStatus.BLOCKED,
                       _ => UserProfileContextMenuControlSettings.FriendshipStatus.NONE,
                   };
        }

        private void OnFriendsButtonClicked(string userAddress, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            switch (friendshipStatus)
            {
                case UserProfileContextMenuControlSettings.FriendshipStatus.NONE:
                    SendFriendRequest(userAddress);
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND:
                    RemoveFriend(userAddress);
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT:
                    CancelFriendRequest(userAddress);
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED:
                    AcceptFriendship(userAddress);
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.BLOCKED: break;
                default: throw new ArgumentOutOfRangeException(nameof(friendshipStatus), friendshipStatus, null);
            }
        }

        private void RemoveFriend(string userAddress)
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            RemoveFriendAsync(cancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid RemoveFriendAsync(CancellationToken ct)
            {
                await mvcManager.ShowAsync(UnfriendConfirmationPopupController.IssueCommand(new UnfriendConfirmationPopupController.Params
                {
                    UserId = new Web3Address(userAddress),
                }), ct);
            }
        }

        private void CancelFriendRequest(string userAddress)
        {
            IFriendsService friendService = friendServiceProxy.Object;
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            CancelFriendRequestThenChangeInteractionStatusAsync(cancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid CancelFriendRequestThenChangeInteractionStatusAsync(CancellationToken ct)
            {
                await friendService.CancelFriendshipAsync(userAddress, ct);
            }
        }

        private void SendFriendRequest(string userAddress)
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            ShowFriendRequestUIAsync(cancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid ShowFriendRequestUIAsync(CancellationToken ct)
            {
                await mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams
                {
                    DestinationUser = new Web3Address(userAddress),
                }), ct);
            }
        }

        private void AcceptFriendship(string userAddress)
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            IFriendsService friendService = friendServiceProxy.Object!;

            AcceptFriendRequestThenChangeInteractionStatusAsync(cancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid AcceptFriendRequestThenChangeInteractionStatusAsync(CancellationToken ct)
            {
                await friendService.AcceptFriendshipAsync(userAddress, ct);
            }
        }

        private void OnShowUserPassportClicked(string userId)
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            closeContextMenuTask.TrySetResult();
            ShowPassport(userId, cancellationTokenSource.Token).Forget();
        }

        private void OnMentionUserClicked(string userName)
        {
            closeContextMenuTask.TrySetResult();

            //Per design request we need to add an extra character after adding the mention to the chat.
            ShowChat(() => chatEventBus.InsertText(userName + " ")).Forget();
        }

        private void OnOpenConversationButtonClicked(string userId)
        {
            closeContextMenuTask.TrySetResult();
            ShowChat(() => chatEventBus.OpenConversationUsingUserId(userId)).Forget();
        }

        private async UniTaskVoid ShowChat(Action onChatShown)
        {
            await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true, true));
            onChatShown?.Invoke();
        }

        private void OnBlockUserClicked(string userId)
        {
            ShowBlockUserPromptAsync(targetProfile).Forget();
        }

        private async UniTaskVoid ShowBlockUserPromptAsync(Profile profile)
        {
            await mvcManager.ShowAsync(BlockUserPromptController.IssueCommand(new BlockUserPromptParams(new Web3Address(profile.UserId), profile.Name, BlockUserPromptParams.UserBlockAction.BLOCK)));
        }

        private void OnJumpInClicked(string userId)
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            FriendListSectionUtilities.JumpToFriendLocation(userId, cancellationTokenSource, getUserPositionBuffer, onlineUsersProvider, realmNavigator, parcel => JumpToFriendClicked(userId, parcel));
        }

        private UniTask ShowPassport(string userId, CancellationToken ct) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(userId)), ct);

        private void JumpToFriendClicked(string targetAddress, Vector2Int parcel) =>
            analytics.Track(AnalyticsEvents.Friends.JUMP_TO_FRIEND_CLICKED, new JsonObject
            {
                { "receiver_id", targetAddress },
                { "friend_position", parcel.ToString() },
            });
    }
}
