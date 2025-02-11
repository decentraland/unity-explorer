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
using UnityEngine;

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

        public MVCManagerMenusAccessFacade(IMVCManager mvcManager, ISystemClipboard systemClipboard, IClipboardManager clipboardManager)
        {
            this.mvcManager = mvcManager;
            this.clipboardManager = clipboardManager;

            //TODO FRAN -> Add proper request friend action here when Friends functionality is merged to dev
            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, null);
            openUserProfileButtonContextMenuControlSettings = new OpenUserProfileButtonContextMenuControlSettings(OnShowUserPassportClicked);
            mentionUserButtonContextMenuControlSettings = new MentionUserButtonContextMenuControlSettings(OnPasteUserClicked);
            contextMenu = new GenericContextMenu().AddControl(userProfileContextMenuControlSettings)
                                                  .AddControl(openUserProfileButtonContextMenuControlSettings)
                                                  .AddControl(mentionUserButtonContextMenuControlSettings);

        }

        public UniTask ShowExternalUrlPromptAsync(string url) =>
            mvcManager.ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(url)));

        public UniTask ShowTeleporterPromptAsync(Vector2Int coords) =>
            mvcManager.ShowAsync(TeleportPromptController.IssueCommand(new TeleportPromptController.Params(coords)));

        public UniTask ShowChangeRealmPromptAsync(string message, string realm) =>
            mvcManager.ShowAsync(ChangeRealmPromptController.IssueCommand(new ChangeRealmPromptController.Params(message, realm)));

        public UniTask ShowPastePopupToastAsync(PastePopupToastData data) =>
            mvcManager.ShowAsync(PastePopupToastController.IssueCommand(data));

        public UniTask ShowChatEntryMenuPopupAsync(ChatEntryMenuPopupData data) =>
            mvcManager.ShowAsync(ChatEntryMenuPopupController.IssueCommand(data));

        public UniTask ShowPassport(string userId) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(userId)));

        public UniTask ShowUserProfileContextMenu(Profile profile, Color userColor, Transform transform)
        {
            closeContextMenuTask?.TrySetResult();
            closeContextMenuTask = new UniTaskCompletionSource();
            userProfileContextMenuControlSettings.SetInitialData(profile, userColor, UserProfileContextMenuControlSettings.FriendshipStatus.NONE);
            mentionUserButtonContextMenuControlSettings.SetData(profile);
            openUserProfileButtonContextMenuControlSettings.SetData(profile);

            return mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                new GenericContextMenuParameter(
                    contextMenu,
                    transform.position,
                    closeTask: closeContextMenuTask.Task
                )));
        }

        private void OnShowUserPassportClicked(Profile data)
        {
            closeContextMenuTask.TrySetResult();
            ShowPassport(data.UserId).Forget();
        }
        private void OnPasteUserClicked(Profile data)
        {
            closeContextMenuTask.TrySetResult();
            clipboardManager.Copy(this, data.MentionName);
            clipboardManager.Paste(this);
        }

    }
}
