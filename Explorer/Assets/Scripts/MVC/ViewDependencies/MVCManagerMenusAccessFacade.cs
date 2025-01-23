using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.ExternalUrlPrompt;
using DCL.TeleportPrompt;
using DCL.UI;
using UnityEngine;

namespace MVC
{
    /// <summary>
    /// Provides access to a limited set of views previously registered in the MVC Manager.
    /// </summary>
    public class MVCManagerMenusAccessFacade
    {
        private readonly IMVCManager mvcManager;

        public MVCManagerMenusAccessFacade(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
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
    }
}
