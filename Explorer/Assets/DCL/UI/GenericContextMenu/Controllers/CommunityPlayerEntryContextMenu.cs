using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.Communities;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UI;
using DCL.Friends.UI.FriendPanel.Sections;
using DCL.Friends.UI.FriendPanel.Sections.Friends;
using DCL.Friends.UI.Requests;
using DCL.Multiplayer.Connectivity;
using DCL.Passport;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles;
using DCL.Profiles.Helpers;
using DCL.UI.ConfirmationDialog.Opener;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.GenericContextMenuParameter;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.VoiceChat;
using DCL.Web3;
using ECS.SceneLifeCycle.Realm;
using MVC;
using Segment.Serialization;
using System;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;
using FriendshipStatus = DCL.Friends.FriendshipStatus;

namespace DCL.UI.GenericContextMenu.Controllers
{
    public class CommunityPlayerEntryContextMenu
    {
        private delegate void StringDelegate(string id);

        private const string BAN_MEMBER_TEXT_FORMAT = "Are you sure you want to ban [{0}] from the [{1}] Community?";
        private const string BAN_MEMBER_CANCEL_TEXT = "CANCEL";
        private const string BAN_MEMBER_CONFIRM_TEXT = "BAN";

        private static readonly Vector2 CONTEXT_MENU_OFFSET = new (5, -10);

        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly ObjectProxy<FriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy;
        private readonly IMVCManager mvcManager;
        private readonly IChatEventBus chatEventBus;
        private readonly IAnalyticsController analytics;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly CommunityVoiceChatContextMenuConfiguration voiceChatContextMenuSettings;
        private readonly CommunitiesDataProvider communityDataProvider;

        private readonly string[] getUserPositionBuffer = new string[1];

        private readonly UI.GenericContextMenuParameter.GenericContextMenu contextMenu;
        private readonly UserProfileContextMenuControlSettings userProfileControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> openUserProfileButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> jumpInButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> openConversationControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> demoteSpeakerButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> promoteToSpeakerButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> kickFromStreamButtonControlSettings;
        private readonly ButtonWithDelegateContextMenuControlSettings<string> banFromCommunityButtonControlSettings;
        private readonly GenericContextMenuElement contextMenuJumpInButton;
        private readonly GenericContextMenuElement demoteSpeakerButton;
        private readonly GenericContextMenuElement promoteToSpeakerButton;
        private readonly GenericContextMenuElement kickFromStreamButton;
        private readonly GenericContextMenuElement banFromCommunityButton;

        private CancellationTokenSource cancellationTokenSource;
        private UniTaskCompletionSource closeContextMenuTask;

        public CommunityPlayerEntryContextMenu(
            ObjectProxy<IFriendsService> friendServiceProxy,
            IChatEventBus chatEventBus,
            IMVCManager mvcManager,
            GenericUserProfileContextMenuSettings contextMenuSettings,
            IAnalyticsController analytics,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            ObjectProxy<FriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy,
            ISharedSpaceManager sharedSpaceManager,
            CommunityVoiceChatContextMenuConfiguration voiceChatContextMenuSettings,
            IVoiceChatOrchestrator voiceChatOrchestrator, CommunitiesDataProvider communityDataProvider)
        {
            this.friendServiceProxy = friendServiceProxy;
            this.chatEventBus = chatEventBus;
            this.mvcManager = mvcManager;
            this.analytics = analytics;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.friendOnlineStatusCacheProxy = friendOnlineStatusCacheProxy;
            this.sharedSpaceManager = sharedSpaceManager;
            this.voiceChatContextMenuSettings = voiceChatContextMenuSettings;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.communityDataProvider = communityDataProvider;

            userProfileControlSettings = new UserProfileContextMenuControlSettings(OnFriendsButtonClicked);
            openUserProfileButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.OpenUserProfileButtonConfig.Text, contextMenuSettings.OpenUserProfileButtonConfig.Sprite, new StringDelegate(OnShowUserPassportClicked));
            jumpInButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.JumpInButtonConfig.Text, contextMenuSettings.JumpInButtonConfig.Sprite, new StringDelegate(OnJumpInClicked));
            openConversationControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(contextMenuSettings.OpenConversationButtonConfig.Text, contextMenuSettings.OpenConversationButtonConfig.Sprite, new StringDelegate(OnOpenConversationButtonClicked));

            demoteSpeakerButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(voiceChatContextMenuSettings.DemoteSpeakerText, voiceChatContextMenuSettings.DemoteSpeakerSprite, new StringDelegate(OnDemoteSpeakerClicked));
            promoteToSpeakerButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(voiceChatContextMenuSettings.PromoteToSpeakerText, voiceChatContextMenuSettings.PromoteToSpeakerSprite, new StringDelegate(OnPromoteToSpeakerClicked));
            kickFromStreamButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(voiceChatContextMenuSettings.KickFromStreamText, voiceChatContextMenuSettings.KickFromStreamSprite, new StringDelegate(OnKickUserClicked));
            banFromCommunityButtonControlSettings = new ButtonWithDelegateContextMenuControlSettings<string>(voiceChatContextMenuSettings.BanUserText, voiceChatContextMenuSettings.BanUserSprite, new StringDelegate(OnBanUserClicked));

            contextMenuJumpInButton = new GenericContextMenuElement(jumpInButtonControlSettings, false);
            demoteSpeakerButton = new GenericContextMenuElement(demoteSpeakerButtonControlSettings, false);
            promoteToSpeakerButton = new GenericContextMenuElement(promoteToSpeakerButtonControlSettings, false);
            kickFromStreamButton = new GenericContextMenuElement(kickFromStreamButtonControlSettings, false);
            banFromCommunityButton = new GenericContextMenuElement(banFromCommunityButtonControlSettings, false);

            contextMenu = new UI.GenericContextMenuParameter.GenericContextMenu(voiceChatContextMenuSettings.ContextMenuWidth, CONTEXT_MENU_OFFSET, voiceChatContextMenuSettings.VerticalPadding, voiceChatContextMenuSettings.ElementsSpacing, anchorPoint: ContextMenuOpenDirection.BOTTOM_RIGHT)
                         .AddControl(userProfileControlSettings)
                         .AddControl(new SeparatorContextMenuControlSettings(voiceChatContextMenuSettings.SeparatorHeight, -voiceChatContextMenuSettings.VerticalPadding.left, -voiceChatContextMenuSettings.VerticalPadding.right))
                         .AddControl(openUserProfileButtonControlSettings)
                         .AddControl(openConversationControlSettings)
                         .AddControl(contextMenuJumpInButton)
                         .AddControl(new SeparatorContextMenuControlSettings(voiceChatContextMenuSettings.SeparatorHeight, -voiceChatContextMenuSettings.VerticalPadding.left, -voiceChatContextMenuSettings.VerticalPadding.right))
                         .AddControl(demoteSpeakerButton)
                         .AddControl(promoteToSpeakerButton)
                         .AddControl(kickFromStreamButton)
                         .AddControl(banFromCommunityButton);
        }

        public async UniTask ShowUserProfileContextMenuAsync(Profile targetProfile, Vector3 position, Vector2 offset,
            CancellationToken ct, UniTask closeMenuTask, Action onContextMenuHide = null,
            ContextMenuOpenDirection anchorPoint = ContextMenuOpenDirection.BOTTOM_RIGHT,
            bool targetIsSpeaker = false)
        {

            var localParticipant = voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState;

            bool targetIsLocalParticipant = targetProfile.UserId == localParticipant.WalletId;
            bool localParticipantIsMod = voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.Role.Value is VoiceChatParticipantsStateService.UserCommunityRoleMetadata.moderator or VoiceChatParticipantsStateService.UserCommunityRoleMetadata.owner;

            closeContextMenuTask?.TrySetResult();
            closeContextMenuTask = new UniTaskCompletionSource();
            UniTask closeTask = UniTask.WhenAny(closeContextMenuTask.Task, closeMenuTask);
            UserProfileContextMenuControlSettings.FriendshipStatus contextMenuFriendshipStatus = UserProfileContextMenuControlSettings.FriendshipStatus.DISABLED;

            if (!targetIsLocalParticipant && friendServiceProxy.Configured)
            {
                FriendshipStatus friendshipStatus = await friendServiceProxy.Object.GetFriendshipStatusAsync(targetProfile.UserId, ct);
                contextMenuFriendshipStatus = ConvertFriendshipStatus(friendshipStatus);
                jumpInButtonControlSettings.SetData(targetProfile.UserId);

                contextMenuJumpInButton.Enabled = friendshipStatus == FriendshipStatus.FRIEND &&
                                                  friendOnlineStatusCacheProxy.Object.GetFriendStatus(targetProfile.UserId) != OnlineStatus.OFFLINE;
            }

            userProfileControlSettings.SetInitialData(targetProfile.ToUserData(), contextMenuFriendshipStatus);

            openUserProfileButtonControlSettings.SetData(targetProfile.UserId);
            openConversationControlSettings.SetData(targetProfile.UserId);
            demoteSpeakerButtonControlSettings.SetData(targetProfile.UserId);
            promoteToSpeakerButtonControlSettings.SetData(targetProfile.UserId);
            kickFromStreamButtonControlSettings.SetData(targetProfile.UserId);
            banFromCommunityButtonControlSettings.SetData(targetProfile.UserId);

            promoteToSpeakerButton.Enabled = !targetIsSpeaker && localParticipantIsMod;
            demoteSpeakerButton.Enabled = targetIsSpeaker && localParticipantIsMod;
            kickFromStreamButton.Enabled = !targetIsLocalParticipant && localParticipantIsMod;
            banFromCommunityButton.Enabled = !targetIsLocalParticipant && localParticipantIsMod;

            contextMenu.ChangeAnchorPoint(anchorPoint);

            if (offset == default(Vector2))
                offset = CONTEXT_MENU_OFFSET;

            contextMenu.ChangeOffsetFromTarget(offset);

            await mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                new GenericContextMenuParameter.GenericContextMenuParameter(contextMenu, position, actionOnHide: onContextMenuHide, closeTask: closeTask)), ct);
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

        private void OnOpenConversationButtonClicked(string userId)
        {
            closeContextMenuTask.TrySetResult();
            ShowChatAsync(() => chatEventBus.OpenPrivateConversationUsingUserId(userId)).Forget();
        }

        private async UniTaskVoid ShowChatAsync(Action onChatShown)
        {
            await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true, true));
            onChatShown?.Invoke();
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

        private void OnDemoteSpeakerClicked(string walletId)
        {
            voiceChatOrchestrator.DemoteFromSpeakerInCurrentCall(walletId);
            closeContextMenuTask.TrySetResult();
        }

        private void OnPromoteToSpeakerClicked(string walletId)
        {
            voiceChatOrchestrator.PromoteToSpeakerInCurrentCall(walletId);
            closeContextMenuTask.TrySetResult();
        }

        private void OnKickUserClicked(string walletId)
        {
            voiceChatOrchestrator.KickPlayerFromCurrentCall(walletId);
            closeContextMenuTask.TrySetResult();
        }

        private void OnBanUserClicked(string walletId)
        {
            closeContextMenuTask.TrySetResult();
            ShowBanConfirmationDialog(walletId);
        }

        private void ShowBanConfirmationDialog(string walletId)
        {
            string currentCommunityId = voiceChatOrchestrator.CurrentCommunityId;
            if (!voiceChatOrchestrator.TryGetActiveCommunityData(currentCommunityId, out var community)) return;

            var participant = voiceChatOrchestrator.ParticipantsStateService.GetParticipantState(walletId);

            if (participant == null) return;

            string communityName = community.communityName;

            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            ShowBanConfirmationDialogAsync(cancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid ShowBanConfirmationDialogAsync(CancellationToken ct)
            {
                Result<ConfirmationResult> dialogResult = await ViewDependencies.ConfirmationDialogOpener.OpenConfirmationDialogAsync(new ConfirmationDialogParameter(
                                                                                         string.Format(BAN_MEMBER_TEXT_FORMAT, participant.Name, communityName),
                                                                                         BAN_MEMBER_CANCEL_TEXT,
                                                                                         BAN_MEMBER_CONFIRM_TEXT,
                                                                                         voiceChatContextMenuSettings.BanUserPopupSprite,
                                                                                         false, false,
                                                                                         userInfo: new ConfirmationDialogParameter.UserData(walletId, participant.ProfilePictureUrl, ProfileNameColorHelper.GetNameColor(participant.Name))),
                                                                                     ct)
                                                                                .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested || !dialogResult.Success || dialogResult.Value == ConfirmationResult.CANCEL) return;

                BanUser(walletId, currentCommunityId);
            }
        }

        private void BanUser(string userWallet, string communityId)
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            BanUserAsync(cancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid BanUserAsync(CancellationToken token)
            {
                await communityDataProvider.BanUserFromCommunityAsync(userWallet, communityId, token)
                                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);
            }
        }
    }
}
