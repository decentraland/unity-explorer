using Cysharp.Threading.Tasks;
using DCL.Chat.InputBus;
using DCL.Friends;
using DCL.Friends.UI;
using DCL.Friends.UI.Requests;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Utilities;
using DCL.Web3;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace MVC
{
    public class GenericUserProfileContextMenuController
    {
        private static readonly RectOffset CONTEXT_MENU_VERTICAL_LAYOUT_PADDING = new (15, 15, 20, 25);
        private static readonly Vector2 CONTEXT_MENU_OFFSET = new (5,-10);
        private const int CONTEXT_MENU_SEPARATOR_HEIGHT = 20;
        private const int CONTEXT_MENU_ELEMENTS_SPACING = 5;
        private const int CONTEXT_MENU_WIDTH = 250;

        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly MentionUserButtonContextMenuControlSettings mentionUserButtonContextMenuControlSettings;
        private readonly OpenUserProfileButtonContextMenuControlSettings openUserProfileButtonContextMenuControlSettings;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly GenericContextMenu contextMenu;
        private readonly IMVCManager mvcManager;
        private readonly IChatInputBus chatInputBus;

        private CancellationTokenSource cancellationTokenSource;
        private UniTaskCompletionSource closeContextMenuTask;

        public GenericUserProfileContextMenuController(ObjectProxy<IFriendsService> friendServiceProxy)
        {
            this.friendServiceProxy = friendServiceProxy;
            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(OnFriendsButtonClicked);
            openUserProfileButtonContextMenuControlSettings = new OpenUserProfileButtonContextMenuControlSettings(OnShowUserPassportClicked);
            mentionUserButtonContextMenuControlSettings = new MentionUserButtonContextMenuControlSettings(OnMentionUserClicked);
            contextMenu = new GenericContextMenu(CONTEXT_MENU_WIDTH, CONTEXT_MENU_OFFSET, CONTEXT_MENU_VERTICAL_LAYOUT_PADDING, CONTEXT_MENU_ELEMENTS_SPACING, anchorPoint: GenericContextMenuAnchorPoint.BOTTOM_LEFT)
                         .AddControl(userProfileContextMenuControlSettings)
                         .AddControl(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right))
                         .AddControl(openUserProfileButtonContextMenuControlSettings)
                         .AddControl(mentionUserButtonContextMenuControlSettings);
        }

        public async UniTask ShowUserProfileContextMenuAsync(Profile profile, Vector3 position, CancellationToken ct, Action onContextMenuHide = null)
        {
            closeContextMenuTask?.TrySetResult();
            closeContextMenuTask = new UniTaskCompletionSource();
            UserProfileContextMenuControlSettings.FriendshipStatus contextMenuFriendshipStatus = UserProfileContextMenuControlSettings.FriendshipStatus.DISABLED;

            if (friendServiceProxy.Configured)
            {
                var friendshipStatus = await friendServiceProxy.Object.GetFriendshipStatusAsync(profile.UserId, ct);
                contextMenuFriendshipStatus = ConvertFriendshipStatus(friendshipStatus);
            }

            //TODO FRAN> FIX THIS
            Sprite thumbnailSprite = null;

            userProfileContextMenuControlSettings.SetInitialData(profile.DisplayName, profile.UserId, profile.HasClaimedName, profile.UserNameColor, contextMenuFriendshipStatus, thumbnailSprite);
            mentionUserButtonContextMenuControlSettings.SetData(profile.DisplayName);
            openUserProfileButtonContextMenuControlSettings.SetData(profile.UserId);

            await mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                new GenericContextMenuParameter(contextMenu, position, actionOnHide:onContextMenuHide, closeTask: closeContextMenuTask.Task)), ct);
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
                       _ => UserProfileContextMenuControlSettings.FriendshipStatus.NONE
                   };
        }

        private void OnFriendsButtonClicked(string userAddress, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            //TODO FRAN Issue #3408: we should only have this logic in one place, not repeated in each place that uses this context menu
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
            chatInputBus.InsertText(userName + " ");
        }

        public UniTask ShowPassport(string userId, CancellationToken ct) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(userId)), ct);

    }
}
