﻿using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.Chat.EventBus;
using DCL.ExternalUrlPrompt;
using DCL.Friends;
using DCL.Multiplayer.Connectivity;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles;
using DCL.TeleportPrompt;
using DCL.UI;
using DCL.UI.GenericContextMenu.Controllers;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Utilities;
using DCL.Web3;
using ECS.SceneLifeCycle.Realm;
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
        private readonly IChatEventBus chatEventBus;
        private readonly GenericUserProfileContextMenuSettings contextMenuSettings;
        private readonly bool includeUserBlocking;
        private readonly IAnalyticsController analytics;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly ObjectProxy<IFriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy;

        private CancellationTokenSource cancellationTokenSource;
        private GenericUserProfileContextMenuController genericUserProfileContextMenuController;
        private ChatOptionsContextMenuController chatOptionsContextMenuController;

        public MVCManagerMenusAccessFacade(
            IMVCManager mvcManager,
            IProfileCache profileCache,
            ObjectProxy<IFriendsService> friendServiceProxy,
            IChatEventBus chatEventBus,
            bool includeUserBlocking,
            GenericUserProfileContextMenuSettings contextMenuSettings,
            bool includeUserBlocking,
            IAnalyticsController analytics,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator, ObjectProxy<IFriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy)
        {
            this.mvcManager = mvcManager;
            this.profileCache = profileCache;
            this.friendServiceProxy = friendServiceProxy;
            this.chatEventBus = chatEventBus;
            this.contextMenuSettings = contextMenuSettings;
            this.includeUserBlocking = includeUserBlocking;
            this.analytics = analytics;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.friendOnlineStatusCacheProxy = friendOnlineStatusCacheProxy;
            genericUserProfileContextMenuController = new GenericUserProfileContextMenuController(friendServiceProxy, chatInputBus, mvcManager, includeUserBlocking);
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

        public async UniTask ShowUserProfileContextMenuFromWalletIdAsync(Web3Address walletId, Vector3 position, Vector2 offset, CancellationToken ct, UniTask closeMenuTask, Action onHide = null, MenuAnchorPoint anchorPoint = MenuAnchorPoint.DEFAULT)
        {
            Profile profile = profileCache.Get(walletId);
            if (profile == null) return;
            await ShowUserProfileContextMenuAsync(profile, position, offset, ct, onHide, closeMenuTask, anchorPoint);
        }

        public async UniTask ShowUserProfileContextMenuFromUserNameAsync(string userName, Vector3 position, Vector2 offset, CancellationToken ct, UniTask closeMenuTask, Action onHide = null)
        {
            Profile profile = profileCache.GetByUserName(userName);
            if (profile == null) return;
            await ShowUserProfileContextMenuAsync(profile, position, offset, ct, onHide, closeMenuTask);
        }

        public async UniTaskVoid ShowChatContextMenuAsync(bool chatBubblesVisibility, Vector3 transformPosition, ChatOptionsContextMenuData data, Action<bool> onToggleChatBubblesVisibility, Action onContextMenuHide, UniTask closeMenuTask)
        {
            chatOptionsContextMenuController ??= new ChatOptionsContextMenuController(mvcManager, data.ChatBubblesToggleIcon, data.ChatBubblesToggleText, data.PinChatToggleTextIcon, data.PinChatToggleText);
            chatOptionsContextMenuController.ChatBubblesVisibilityChanged = null;
            chatOptionsContextMenuController.ChatBubblesVisibilityChanged += onToggleChatBubblesVisibility;
            await chatOptionsContextMenuController.ShowContextMenuAsync(chatBubblesVisibility, transformPosition, closeMenuTask, onContextMenuHide);
        }

        private async UniTask ShowUserProfileContextMenuAsync(Profile profile, Vector3 position, Vector2 offset, CancellationToken ct, Action onContextMenuHide, UniTask closeMenuTask, MenuAnchorPoint anchorPoint = MenuAnchorPoint.DEFAULT)
        {
            genericUserProfileContextMenuController ??= new GenericUserProfileContextMenuController(friendServiceProxy, chatEventBus, mvcManager, contextMenuSettings, analytics, includeUserBlocking, onlineUsersProvider, realmNavigator, friendOnlineStatusCacheProxy);
            await genericUserProfileContextMenuController.ShowUserProfileContextMenuAsync(profile, position, offset, ct, closeMenuTask, onContextMenuHide, ConvertMenuAnchorPoint(anchorPoint));
        }

        private GenericContextMenuAnchorPoint ConvertMenuAnchorPoint(MenuAnchorPoint anchorPoint)
        {
            switch (anchorPoint)
            {
                case MenuAnchorPoint.TOP_LEFT:
                    return GenericContextMenuAnchorPoint.TOP_LEFT;
                case MenuAnchorPoint.TOP_RIGHT:
                    return GenericContextMenuAnchorPoint.TOP_RIGHT;
                case MenuAnchorPoint.BOTTOM_LEFT:
                    return GenericContextMenuAnchorPoint.BOTTOM_LEFT;
                case MenuAnchorPoint.BOTTOM_RIGHT:
                    return GenericContextMenuAnchorPoint.BOTTOM_RIGHT;
                case MenuAnchorPoint.CENTER_LEFT:
                    return GenericContextMenuAnchorPoint.CENTER_LEFT;
                case MenuAnchorPoint.CENTER_RIGHT:
                    return GenericContextMenuAnchorPoint.CENTER_RIGHT;
                default:
                case MenuAnchorPoint.DEFAULT:
                    return GenericContextMenuAnchorPoint.DEFAULT;
            }
        }
    }
}
