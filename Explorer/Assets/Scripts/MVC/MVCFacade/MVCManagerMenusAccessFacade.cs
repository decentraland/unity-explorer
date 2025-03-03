using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.ExternalUrlPrompt;
using DCL.Profiles;
using DCL.TeleportPrompt;
using DCL.UI;
using System;
using System.Threading;
using UnityEngine;

namespace MVC
{
    /// <summary>
    /// Provides access to a limited set of views previously registered in the MVC Manager. This allows views without controllers to a restricted MVC
    /// </summary>
    public class MVCManagerMenusAccessFacade : IMVCManagerMenusAccessFacade
    {
        private readonly IMVCManager mvcManager;
        private readonly IProfileCache profileCache;
        private readonly GenericUserProfileContextMenuController genericUserProfileContextMenuController;

        private UniTaskCompletionSource closeContextMenuTask;
        private CancellationTokenSource cancellationTokenSource;

        public MVCManagerMenusAccessFacade(
            IMVCManager mvcManager,
            IProfileCache profileCache
            )
        {
            this.mvcManager = mvcManager;
            this.profileCache = profileCache;
        }

        public async UniTask ShowExternalUrlPromptAsync(string url, CancellationToken ct) =>
            await mvcManager.ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(url)), ct);

        public async UniTask ShowTeleporterPromptAsync(Vector2Int coords, CancellationToken ct) =>
            await mvcManager.ShowAsync(TeleportPromptController.IssueCommand(new TeleportPromptController.Params(coords)), ct);

        public async UniTask ShowChangeRealmPromptAsync(string message, string realm, CancellationToken ct) =>
            await mvcManager.ShowAsync(ChangeRealmPromptController.IssueCommand(new ChangeRealmPromptController.Params(message, realm)), ct);

        public async UniTask ShowPastePopupToastAsync(PastePopupToastData data, CancellationToken ct) =>
            await mvcManager.ShowAsync(PastePopupToastController.IssueCommand(data), ct);

        public async UniTask ShowChatEntryMenuPopupAsync(ChatEntryMenuPopupData data, CancellationToken ct) =>
            await mvcManager.ShowAsync(ChatEntryMenuPopupController.IssueCommand(data), ct);


        public async UniTask ShowUserProfileContextMenuAsync(Profile profile, Vector3 position, CancellationToken ct, Action onContextMenuHide = null)
        {
            closeContextMenuTask?.TrySetResult();
            closeContextMenuTask = new UniTaskCompletionSource();


        }

        public async UniTask ShowUserProfileContextMenuFromWalletIdAsync(string walletId, Vector3 position, CancellationToken ct, Action onHide = null)
        {
            Profile profile = profileCache.Get(walletId);
            if (profile == null) return;
            await ShowUserProfileContextMenuAsync(profile, position, ct, onHide);
        }


        public async UniTask ShowUserProfileContextMenuFromUserNameAsync(string userName, Vector3 position, CancellationToken ct)
        {
            Profile profile = profileCache.GetByUserName(userName);
            if (profile == null) return;
            await ShowUserProfileContextMenuAsync(profile, position, ct );
        }
    }
}
