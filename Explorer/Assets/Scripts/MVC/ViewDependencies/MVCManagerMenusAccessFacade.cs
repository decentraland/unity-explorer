using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.Clipboard;
using DCL.ExternalUrlPrompt;
using DCL.Friends;
using DCL.Passport;
using DCL.Profiles;
using DCL.TeleportPrompt;
using DCL.UI;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Utilities;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace MVC
{
    /// <summary>
    /// Provides access to a limited set of views previously registered in the MVC Manager. This allows views without controllers to a restricted MVC
    /// </summary>
    public class MVCManagerMenusAccessFacade
    {
        private readonly IMVCManager mvcManager;
        private readonly GenericContextMenu contextMenu;
        private readonly IClipboardManager clipboardManager;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly IProfileCache profileCache;

        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly MentionUserButtonContextMenuControlSettings mentionUserButtonContextMenuControlSettings;
        private readonly OpenUserProfileButtonContextMenuControlSettings openUserProfileButtonContextMenuControlSettings;
        private UniTaskCompletionSource closeContextMenuTask;
        private CancellationTokenSource cancellationTokenSource;

        public MVCManagerMenusAccessFacade(IMVCManager mvcManager, ISystemClipboard systemClipboard, IClipboardManager clipboardManager, ObjectProxy<IFriendsService> friendServiceProxy, IProfileCache profileCache)
        {
            this.mvcManager = mvcManager;
            this.clipboardManager = clipboardManager;
            this.friendServiceProxy = friendServiceProxy;
            this.profileCache = profileCache;

            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, friendServiceProxy.Configured? OnFriendsButtonClicked : null);
            openUserProfileButtonContextMenuControlSettings = new OpenUserProfileButtonContextMenuControlSettings(OnShowUserPassportClicked);
            mentionUserButtonContextMenuControlSettings = new MentionUserButtonContextMenuControlSettings(OnPasteUserClicked);
            contextMenu = new GenericContextMenu(230, new Vector2(5,-10), anchorPoint: GenericContextMenuAnchorPoint.BOTTOM_LEFT)
                         .AddControl(userProfileContextMenuControlSettings)
                         .AddControl(new SeparatorContextMenuControlSettings())
                         .AddControl(openUserProfileButtonContextMenuControlSettings)
                         .AddControl(mentionUserButtonContextMenuControlSettings);
        }

        public UniTask ShowExternalUrlPromptAsync(string url, CancellationToken ct) =>
            mvcManager.ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(url)), ct);

        public UniTask ShowTeleporterPromptAsync(Vector2Int coords, CancellationToken ct) =>
            mvcManager.ShowAsync(TeleportPromptController.IssueCommand(new TeleportPromptController.Params(coords)), ct);

        public UniTask ShowChangeRealmPromptAsync(string message, string realm, CancellationToken ct) =>
            mvcManager.ShowAsync(ChangeRealmPromptController.IssueCommand(new ChangeRealmPromptController.Params(message, realm)), ct);

        public UniTask ShowPastePopupToastAsync(PastePopupToastData data, CancellationToken ct) =>
            mvcManager.ShowAsync(PastePopupToastController.IssueCommand(data), ct);

        public UniTask ShowChatEntryMenuPopupAsync(ChatEntryMenuPopupData data, CancellationToken ct) =>
            mvcManager.ShowAsync(ChatEntryMenuPopupController.IssueCommand(data), ct);

        public UniTask ShowPassport(string userId, CancellationToken ct) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(userId)), ct);

        public async UniTask ShowUserProfileContextMenuAsync(Profile profile, Vector3 position, CancellationToken ct)
        {
            closeContextMenuTask?.TrySetResult();
            closeContextMenuTask = new UniTaskCompletionSource();

            FriendshipStatus friendshipStatus = FriendshipStatus.NONE;
            if (friendServiceProxy.Configured)
                friendshipStatus = await friendServiceProxy.Object.GetFriendshipStatusAsync(profile.DisplayName, ct);

            userProfileContextMenuControlSettings.SetInitialData(profile.DisplayName, profile.WalletId, profile.HasClaimedName,profile.UserNameColor, ConvertFriendshipStatus(friendshipStatus));
            mentionUserButtonContextMenuControlSettings.SetData(profile);
            openUserProfileButtonContextMenuControlSettings.SetData(profile);

            await mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                new GenericContextMenuParameter(contextMenu, position, closeTask: closeContextMenuTask.Task)), ct);
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

        private void OnFriendsButtonClicked(string s, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            switch (friendshipStatus)
            {
                case UserProfileContextMenuControlSettings.FriendshipStatus.NONE: break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND: break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT: break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED: break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.BLOCKED: break;
                default: throw new ArgumentOutOfRangeException(nameof(friendshipStatus), friendshipStatus, null);
            }

        }


        private void OnShowUserPassportClicked(Profile data)
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            closeContextMenuTask.TrySetResult();
            ShowPassport(data.UserId, cancellationTokenSource.Token).Forget();
        }
        private void OnPasteUserClicked(Profile data)
        {
            closeContextMenuTask.TrySetResult();
            //Per design request we need to add an extra character after adding the mention to the chat.
            clipboardManager.Copy(this, data.MentionName + " ");
            clipboardManager.Paste(this);
        }


        public async UniTask ShowUserProfileContextMenuFromWalledIdAsync(string walletId, Vector3 position, CancellationToken ct)
        {
            Profile profile = profileCache.Get(walletId);
            if (profile == null) return;
            await ShowUserProfileContextMenuAsync(profile, position, ct );
        }


        public async UniTask ShowUserProfileContextMenuFromUserNameAsync(string userName, Vector3 position, CancellationToken ct)
        {
            Profile profile = profileCache.GetByUserName(userName);
            if (profile == null) return;
            await ShowUserProfileContextMenuAsync(profile, position, ct );
        }
    }
}
