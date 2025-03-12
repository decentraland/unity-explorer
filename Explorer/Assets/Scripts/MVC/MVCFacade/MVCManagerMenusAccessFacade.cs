using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.Chat.InputBus;
using DCL.ExternalUrlPrompt;
using DCL.Friends;
using DCL.Profiles;
using DCL.TeleportPrompt;
using DCL.UI;
using DCL.UI.GenericContextMenu.Controllers;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Utilities;
using DCL.Web3;
using System;
using System.Threading;
using UnityEngine;

namespace MVC
{
    /// <summary>
    ///     Provides access to a limited set of views previously registered in the MVC Manager. This allows views without controllers to a restricted MVC
    /// </summary>
    public class MVCManagerMenusAccessFacade : IMVCManagerMenusAccessFacade
    {
        private readonly IMVCManager mvcManager;
        private readonly IProfileCache profileCache;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly IChatInputBus chatInputBus;
        private readonly GenericUserProfileContextMenuSettings contextMenuSettings;

        private CancellationTokenSource cancellationTokenSource;
        private GenericUserProfileContextMenuController genericUserProfileContextMenuController;
        private ChatOptionsContextMenuController chatOptionsContextMenuController;

        public MVCManagerMenusAccessFacade(
            IMVCManager mvcManager,
            IProfileCache profileCache,
            ObjectProxy<IFriendsService> friendServiceProxy,
            IChatInputBus chatInputBus,
            GenericUserProfileContextMenuSettings contextMenuSettings
        )
        {
            this.mvcManager = mvcManager;
            this.profileCache = profileCache;
            this.friendServiceProxy = friendServiceProxy;
            this.chatInputBus = chatInputBus;
            this.contextMenuSettings = contextMenuSettings;
        }

        public async UniTask ShowExternalUrlPromptAsync(URLAddress url, CancellationToken ct) =>
            await mvcManager.ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(url)), ct);

        public async UniTask ShowTeleporterPromptAsync(Vector2Int coords, CancellationToken ct) =>
            await mvcManager.ShowAsync(TeleportPromptController.IssueCommand(new TeleportPromptController.Params(coords)), ct);

        public async UniTask ShowChangeRealmPromptAsync(string message, string realm, CancellationToken ct) =>
            await mvcManager.ShowAsync(ChangeRealmPromptController.IssueCommand(new ChangeRealmPromptController.Params(message, realm)), ct);

        public async UniTask ShowPastePopupToastAsync(PastePopupToastData data, CancellationToken ct) =>
            await mvcManager.ShowAsync(PastePopupToastController.IssueCommand(data), ct);

        public async UniTask ShowChatEntryMenuPopupAsync(ChatEntryMenuPopupData data, CancellationToken ct) =>
            await mvcManager.ShowAsync(ChatEntryMenuPopupController.IssueCommand(data), ct);

        public async UniTask ShowUserProfileContextMenuFromWalletIdAsync(Web3Address walletId, Vector3 position, CancellationToken ct, Action onHide = null)
        {
            Profile profile = profileCache.Get(walletId);
            if (profile == null) return;
            await ShowUserProfileContextMenuAsync(profile, position, ct, onHide);
        }

        public async UniTask ShowUserProfileContextMenuFromUserNameAsync(string userName, Vector3 position, CancellationToken ct, Action onHide = null)
        {
            Profile profile = profileCache.GetByUserName(userName);
            if (profile == null) return;
            await ShowUserProfileContextMenuAsync(profile, position, ct, onHide);
        }

        public async UniTaskVoid ShowChatContextMenuAsync(bool chatBubblesVisibility, Vector3 transformPosition, ChatOptionsContextMenuData data, Action<bool> onToggleChatBubblesVisibility, Action onContextMenuHide)
        {
            chatOptionsContextMenuController ??= new ChatOptionsContextMenuController(mvcManager, data.ChatBubblesToggleIcon, data.ChatBubblesToggleText, data.PinChatToggleTextIcon, data.PinChatToggleText);
            chatOptionsContextMenuController.ChatBubblesVisibilityChanged = null;
            chatOptionsContextMenuController.ChatBubblesVisibilityChanged += onToggleChatBubblesVisibility;
            await chatOptionsContextMenuController.ShowContextMenuAsync(chatBubblesVisibility, transformPosition, onContextMenuHide);
        }

        private async UniTask ShowUserProfileContextMenuAsync(Profile profile, Vector3 position, CancellationToken ct, Action onContextMenuHide)
        {
            genericUserProfileContextMenuController ??= new GenericUserProfileContextMenuController(friendServiceProxy, chatInputBus, mvcManager, contextMenuSettings);
            await genericUserProfileContextMenuController.ShowUserProfileContextMenuAsync(profile, position, ct, onContextMenuHide);
        }
    }
}
