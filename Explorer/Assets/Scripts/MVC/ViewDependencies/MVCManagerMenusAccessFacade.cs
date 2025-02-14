using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.Clipboard;
using DCL.ExternalUrlPrompt;
using DCL.Passport;
using DCL.Profiles;
using DCL.TeleportPrompt;
using DCL.UI;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using System.Threading;
using UnityEngine;
using Utility;

namespace MVC
{
    /// <summary>
    /// Provides access to a limited set of views previously registered in the MVC Manager.
    /// </summary>
    public class MVCManagerMenusAccessFacade
    {
        private readonly IMVCManager mvcManager;
        private readonly GenericContextMenu contextMenu;
        private readonly IClipboardManager clipboardManager;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly MentionUserButtonContextMenuControlSettings mentionUserButtonContextMenuControlSettings;
        private readonly OpenUserProfileButtonContextMenuControlSettings openUserProfileButtonContextMenuControlSettings;
        private UniTaskCompletionSource closeContextMenuTask;
        private CancellationTokenSource cancellationTokenSource;

        public MVCManagerMenusAccessFacade(IMVCManager mvcManager, ISystemClipboard systemClipboard, IClipboardManager clipboardManager)
        {
            this.mvcManager = mvcManager;
            this.clipboardManager = clipboardManager;

            //TODO FRAN -> Add proper request friend action here when Friends functionality is merged to dev
            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, null);
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

        public UniTask ShowUserProfileContextMenu(Profile profile, Vector3 position, CancellationToken ct)
        {
            closeContextMenuTask?.TrySetResult();
            closeContextMenuTask = new UniTaskCompletionSource();
            userProfileContextMenuControlSettings.SetInitialData(profile, profile.UserNameColor, UserProfileContextMenuControlSettings.FriendshipStatus.NONE);
            mentionUserButtonContextMenuControlSettings.SetData(profile);
            openUserProfileButtonContextMenuControlSettings.SetData(profile);

            return mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                new GenericContextMenuParameter(contextMenu, position, closeTask: closeContextMenuTask.Task)), ct);
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
            //We need to add an extra character after adding the mention to the chat.
            clipboardManager.Copy(this, data.MentionName + " ");
            clipboardManager.Paste(this);
        }
    }
}
