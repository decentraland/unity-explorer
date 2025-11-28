using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.EmotesWheel.Params;
using DCL.Friends;
using DCL.Friends.UI;
using DCL.Friends.UI.BlockUserPrompt;
using DCL.Friends.UI.FriendPanel.Sections;
using DCL.Friends.UI.FriendPanel.Sections.Friends;
using DCL.Friends.UI.Requests;
using DCL.Multiplayer.Connectivity;
using DCL.Passport;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles;
using DCL.UI.Controls.Configs;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.VoiceChat;
using DCL.Web3;
using ECS.SceneLifeCycle.Realm;
using MVC;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;
using Utility;
using FriendshipStatus = DCL.Friends.FriendshipStatus;

namespace DCL.UI
{
    public class GenericUserProfileContextMenuController
    {
        private delegate void StringDelegate(string id);

        private const int CONTEXT_MENU_SEPARATOR_HEIGHT = 20;
        private const int CONTEXT_MENU_ELEMENTS_SPACING = 5;
        private const int CONTEXT_MENU_WIDTH = 250;
        private static readonly RectOffset CONTEXT_MENU_VERTICAL_LAYOUT_PADDING = new (15, 15, 20, 25);
        private static readonly Vector2 CONTEXT_MENU_OFFSET = new (5, -10);
        private static readonly Vector2 SUBMENU_CONTEXT_MENU_OFFSET = new (0, -30);

        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly ObjectProxy<FriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy;
        private readonly IMVCManager mvcManager;
        private readonly IChatEventBus chatEventBus;
        private readonly bool includeUserBlocking;
        private readonly IAnalyticsController analytics;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly bool includeVoiceChat;
        private readonly bool includeCommunities;
        private readonly IVoiceChatOrchestratorActions voiceChatOrchestrator;

        private readonly string[] getUserPositionBuffer = new string[1];

        private readonly GenericContextMenu contextMenu;
        private readonly UserProfileContextMenuControlSettings userProfileControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> openUserProfileButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> mentionUserButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> jumpInButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> blockButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> openConversationControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> startCallButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> socialEmoteButtonControlSettings;

        private readonly GenericContextMenuElement contextMenuJumpInButton;
        private readonly GenericContextMenuElement contextMenuBlockUserButton;
        private readonly GenericContextMenuElement contextMenuCallButton;
        private readonly GenericContextMenuElement contextMenuSocialEmoteButton;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private CancellationTokenSource cancellationTokenSource;
        private UniTaskCompletionSource closeContextMenuTask;
        private Profile targetProfile;

        private readonly CommunityInvitationContextMenuButtonHandler invitationButtonHandler;

        public GenericUserProfileContextMenuController(
            ObjectProxy<IFriendsService> friendServiceProxy,
            IChatEventBus chatEventBus,
            IMVCManager mvcManager,
            GenericUserProfileContextMenuSettings contextMenuSettings,
            IAnalyticsController analytics,
            bool includeUserBlocking,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            ObjectProxy<FriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy,
            ISharedSpaceManager sharedSpaceManager,
            bool includeCommunities,
            CommunitiesDataProvider communitiesDataProvider, IVoiceChatOrchestratorActions voiceChatOrchestrator)
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
            this.includeVoiceChat = FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT);
            this.includeUserBlocking = includeUserBlocking;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.includeCommunities = includeCommunities;
            this.voiceChatOrchestrator = voiceChatOrchestrator;

            userProfileControlSettings = new UserProfileContextMenuControlSettings(OnFriendsButtonClicked);
            openUserProfileButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.OpenUserProfileButtonConfig.Text, contextMenuSettings.OpenUserProfileButtonConfig.Sprite, new StringDelegate(OnShowUserPassportClicked));
            mentionUserButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.MentionButtonConfig.Text, contextMenuSettings.MentionButtonConfig.Sprite, new StringDelegate(OnMentionUserClicked));
            jumpInButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.JumpInButtonConfig.Text, contextMenuSettings.JumpInButtonConfig.Sprite, new StringDelegate(OnJumpInClicked));
            blockButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.BlockButtonConfig.Text, contextMenuSettings.BlockButtonConfig.Sprite, new StringDelegate(OnBlockUserClicked));
            openConversationControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.OpenConversationButtonConfig.Text, contextMenuSettings.OpenConversationButtonConfig.Sprite, new StringDelegate(OnOpenConversationButtonClicked));
            startCallButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.StartCallButtonConfig.Text, contextMenuSettings.StartCallButtonConfig.Sprite, new StringDelegate(OnStartCallButtonClicked));
            socialEmoteButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.SocialEmoteButtonConfig.Text, contextMenuSettings.SocialEmoteButtonConfig.Sprite, new StringDelegate(OnSocialEmoteButtonClicked));

            contextMenuJumpInButton = new GenericContextMenuElement(jumpInButtonControlSettings, false);
            contextMenuBlockUserButton = new GenericContextMenuElement(blockButtonControlSettings, false);
            contextMenuCallButton = new GenericContextMenuElement(startCallButtonControlSettings, false);
            contextMenuSocialEmoteButton = new GenericContextMenuElement(socialEmoteButtonControlSettings, false);

            contextMenu = new GenericContextMenu(CONTEXT_MENU_WIDTH, SUBMENU_CONTEXT_MENU_OFFSET, CONTEXT_MENU_VERTICAL_LAYOUT_PADDING, CONTEXT_MENU_ELEMENTS_SPACING, anchorPoint: ContextMenuOpenDirection.BOTTOM_RIGHT)
                         .AddControl(userProfileControlSettings)
                         .AddControl(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right))
                         .AddControl(mentionUserButtonControlSettings)
                         .AddControl(openUserProfileButtonControlSettings)
                         .AddControl(openConversationControlSettings)
                         .AddControl(contextMenuCallButton)
                         .AddControl(contextMenuSocialEmoteButton)
                         .AddControl(contextMenuJumpInButton)
                         .AddControl(contextMenuBlockUserButton);

            if (includeCommunities)
            {
                invitationButtonHandler = new CommunityInvitationContextMenuButtonHandler(communitiesDataProvider, CONTEXT_MENU_ELEMENTS_SPACING);
                invitationButtonHandler.AddSubmenuControlToContextMenu(contextMenu, new Vector2(0.0f, contextMenu.offsetFromTarget.y), contextMenuSettings.InviteToCommunityConfig.Text, contextMenuSettings.InviteToCommunityConfig.Sprite);
            }
        }

        public async UniTask ShowUserProfileContextMenuAsync(Profile profile, Vector3 position, Vector2 offset,
            CancellationToken ct, UniTask closeMenuTask, Action onContextMenuHide = null,
            ContextMenuOpenDirection anchorPoint = ContextMenuOpenDirection.BOTTOM_RIGHT, Action onContextMenuShow = null, bool enableSocialEmotes = false)
        {
            closeContextMenuTask?.TrySetResult();
            closeContextMenuTask = new UniTaskCompletionSource();
            UniTask closeTask = UniTask.WhenAny(closeContextMenuTask.Task, closeMenuTask);
            UserProfileContextMenuControlSettings.FriendshipStatus contextMenuFriendshipStatus = UserProfileContextMenuControlSettings.FriendshipStatus.DISABLED;
            targetProfile = profile;

            if (friendServiceProxy.Configured)
            {
                Result<FriendshipStatus> friendshipStatusAsyncResult = await friendServiceProxy.Object.GetFriendshipStatusAsync(profile.UserId, ct)
                                                                                    .SuppressToResultAsync(ReportCategory.FRIENDS);

                if (!friendshipStatusAsyncResult.Success)
                {
                    contextMenuBlockUserButton.Enabled = false;
                    contextMenuJumpInButton.Enabled = false;
                }
                else
                {
                    FriendshipStatus friendshipStatus = friendshipStatusAsyncResult.Value;

                    contextMenuFriendshipStatus = ConvertFriendshipStatus(friendshipStatus);

                    blockButtonControlSettings.SetData(profile.UserId);
                    jumpInButtonControlSettings.SetData(profile.UserId);

                    contextMenuBlockUserButton.Enabled = includeUserBlocking && friendshipStatus != FriendshipStatus.BLOCKED;
                    contextMenuJumpInButton.Enabled = friendshipStatus == FriendshipStatus.FRIEND &&
                                                      friendOnlineStatusCacheProxy.Object.GetFriendStatus(profile.UserId) != OnlineStatus.OFFLINE;
                }
            }

            contextMenuSocialEmoteButton.Enabled = enableSocialEmotes;

            userProfileControlSettings.SetInitialData(profile.ToUserData(), contextMenuFriendshipStatus);

            mentionUserButtonControlSettings.SetData(profile.MentionName);
            openUserProfileButtonControlSettings.SetData(profile.UserId);
            openConversationControlSettings.SetData(profile.UserId);

            if (includeVoiceChat)
            {
                contextMenuCallButton.Enabled = includeVoiceChat;
                startCallButtonControlSettings.SetData(profile.UserId);
            }

            contextMenu.ChangeAnchorPoint(anchorPoint);

            if (offset == default(Vector2))
                offset = CONTEXT_MENU_OFFSET;

            contextMenu.ChangeOffsetFromTarget(offset);

            if (includeCommunities)
                invitationButtonHandler.SetUserToInvite(profile.UserId);

            if (ct.IsCancellationRequested) return;

            await mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                new GenericContextMenuParameter(contextMenu, position, actionOnHide: onContextMenuHide, actionOnShow: onContextMenuShow, closeTask: closeTask)), ct);
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

        private void OnFriendsButtonClicked(UserProfileContextMenuControlSettings.UserData userData, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            switch (friendshipStatus)
            {
                case UserProfileContextMenuControlSettings.FriendshipStatus.NONE:
                    SendFriendRequest(userData.userAddress);
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND:
                    RemoveFriend(userData.userAddress);
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT:
                    CancelFriendRequest(userData.userAddress);
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED:
                    AcceptFriendship(userData.userAddress);
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
                await friendService.CancelFriendshipAsync(userAddress, ct).SuppressToResultAsync(ReportCategory.FRIENDS);
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

            ShowChatAsync(() =>
            {
                chatEventBus.InsertText(userName + " ");
            }).Forget();
        }

        private void OnOpenConversationButtonClicked(string userId)
        {
            closeContextMenuTask.TrySetResult();
            ShowChatAsync(() => chatEventBus.OpenPrivateConversationUsingUserId(userId)).Forget();
        }

        private async UniTaskVoid ShowChatAsync(Action onChatShown)
        {
            await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatMainSharedAreaControllerShowParams(true, true), PanelsSharingSpace.Chat);
            onChatShown?.Invoke();
        }

        private void OnStartCallButtonClicked(string userId)
        {
            closeContextMenuTask.TrySetResult();
            StartCallAsync(userId).Forget();
        }

        private async UniTaskVoid StartCallAsync(string userId)
        {
            await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatMainSharedAreaControllerShowParams(true, true));
            voiceChatOrchestrator.StartPrivateCallWithUserId(userId);
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

        private void OnSocialEmoteButtonClicked(string userId)
        {
            // TODO FriendsPushNotifications
            sharedSpaceManager.ShowAsync(PanelsSharingSpace.EmotesWheel,
                                         new EmotesWheelParams()
                                         {
                                             IsDirectedEmote = true,
                                             TargetUsername = targetProfile.ValidatedName,
                                             TargetUsernameColor = targetProfile.UserNameColor,
                                             TargetWalletAddress = targetProfile.UserId
                                         });
            closeContextMenuTask.TrySetResult();
        }

        private UniTask ShowPassport(string userId, CancellationToken ct) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportParams(userId)), ct);

        private void JumpToFriendClicked(string targetAddress, Vector2Int parcel) =>
            analytics.Track(AnalyticsEvents.Friends.JUMP_TO_FRIEND_CLICKED, new JObject
            {
                { "receiver_id", targetAddress },
                { "friend_position", parcel.ToString() },
            });
    }
}
