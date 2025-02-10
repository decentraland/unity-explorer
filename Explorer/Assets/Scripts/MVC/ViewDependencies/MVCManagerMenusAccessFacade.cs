using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.Clipboard;
using DCL.ExternalUrlPrompt;
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
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;

        public MVCManagerMenusAccessFacade(IMVCManager mvcManager, ISystemClipboard systemClipboard)
        {
            this.mvcManager = mvcManager;

            //TODO FRAN -> Add proper request friend action here when Friends functionality is merged to dev
            //TODO FRAN URGENT -> Add here button to MENTION user in chat.
            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, null);
            contextMenu = new GenericContextMenu().AddControl(userProfileContextMenuControlSettings);
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

        public UniTask ShowUserProfileContextMenu(Profile profile, Color userColor, Transform transform)
        {
            userProfileContextMenuControlSettings.SetInitialData(profile, userColor, UserProfileContextMenuControlSettings.FriendshipStatus.NONE);

            return mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                new GenericContextMenuParameter(contextMenu, transform.position
                )));
        }
    }
}
