﻿using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.Chat.EventBus;
using DCL.Communities;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Diagnostics;
using DCL.ExternalUrlPrompt;
using DCL.Friends;
using DCL.Multiplayer.Connectivity;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles;
using DCL.TeleportPrompt;
using DCL.UI;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.VoiceChat;
using DCL.Web3;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Threading;
using UnityEngine;
using DCL.Passport;

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
        private readonly ObjectProxy<FriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy;
        private readonly IProfileRepository profileRepository;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly CommunityVoiceChatContextMenuConfiguration voiceChatContextMenuSettings;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly bool includeCommunities;
        private readonly CommunitiesDataProvider communitiesDataProvider;

        private CancellationTokenSource cancellationTokenSource;
        private GenericUserProfileContextMenuController genericUserProfileContextMenuController;
        private CommunityPlayerEntryContextMenu? communityPlayerEntryContextMenu;
        private ChatOptionsContextMenuController chatOptionsContextMenuController;
        private CommunityContextMenuController communityContextMenuController;

        public MVCManagerMenusAccessFacade(
            IMVCManager mvcManager,
            IProfileCache profileCache,
            ObjectProxy<IFriendsService> friendServiceProxy,
            IChatEventBus chatEventBus,
            GenericUserProfileContextMenuSettings contextMenuSettings,
            bool includeUserBlocking,
            IAnalyticsController analytics,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator, ObjectProxy<FriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy,
            IProfileRepository profileRepository,
            ISharedSpaceManager sharedSpaceManager,
            CommunityVoiceChatContextMenuConfiguration voiceChatContextMenuSettings,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            bool includeCommunities,
            CommunitiesDataProvider communitiesDataProvider)
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
            this.profileRepository = profileRepository;
            this.sharedSpaceManager = sharedSpaceManager;
            this.voiceChatContextMenuSettings = voiceChatContextMenuSettings;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.communitiesDataProvider = communitiesDataProvider;
            this.includeCommunities = includeCommunities;
            this.communitiesDataProvider = communitiesDataProvider;
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

        public async UniTask ShowUserProfileContextMenuFromWalletIdAsync(Web3Address walletId, Vector3 position, Vector2 offset, CancellationToken ct, UniTask closeMenuTask,
            Action onHide = null, MenuAnchorPoint anchorPoint = MenuAnchorPoint.DEFAULT, Action onShow = null)
        {
            Profile profile = await profileRepository.GetAsync(walletId, ct);

            if (profile == null)
                return;

            await ShowUserProfileContextMenuAsync(profile, position, offset, ct, onHide, onShow, closeMenuTask, anchorPoint);
        }

        public async UniTask ShowCommunityPlayerEntryContextMenuAsync(string participantWalletId, bool isSpeaker, Vector3 position, Vector2 offset, CancellationToken ct, UniTask closeMenuTask, Action onHide = null, MenuAnchorPoint anchorPoint = MenuAnchorPoint.DEFAULT)
        {
            if (string.IsNullOrEmpty(participantWalletId)) return;

            Web3Address walletId = new Web3Address(participantWalletId);
            Profile profile = await profileRepository.GetAsync(walletId, ct);

            if (profile == null) return;

            await ShowCommunityPlayerEntryContextMenuAsync(profile, position, offset, ct, onHide, closeMenuTask, anchorPoint, isSpeaker);
        }

        public async UniTask ShowUserProfileContextMenuFromUserNameAsync(string userName, Vector3 position, Vector2 offset, CancellationToken ct, UniTask closeMenuTask,
            Action onHide = null, Action onShow = null)
        {
            Profile profile = profileCache.GetByUserName(userName);
            if (profile == null) return;
            await ShowUserProfileContextMenuAsync(profile, position, offset, ct, onHide, onShow, closeMenuTask);
        }

        public async UniTaskVoid ShowChatContextMenuAsync(Vector3 transformPosition, ChatOptionsContextMenuData data, Action onDeleteChatHistoryClicked, Action onContextMenuHide, UniTask closeMenuTask)
        {
            chatOptionsContextMenuController ??= new ChatOptionsContextMenuController(mvcManager, data.DeleteChatHistoryIcon, data.DeleteChatHistoryText, onDeleteChatHistoryClicked);
            await chatOptionsContextMenuController.ShowContextMenuAsync(transformPosition, closeMenuTask, onContextMenuHide);
        }

        private async UniTask ShowUserProfileContextMenuAsync(Profile profile, Vector3 position, Vector2 offset, CancellationToken ct, Action onContextMenuHide, Action onContextMenuShow,
            UniTask closeMenuTask, MenuAnchorPoint anchorPoint = MenuAnchorPoint.DEFAULT)
        {
            genericUserProfileContextMenuController ??= new GenericUserProfileContextMenuController(friendServiceProxy, chatEventBus, mvcManager, contextMenuSettings, analytics, includeUserBlocking, onlineUsersProvider, realmNavigator, friendOnlineStatusCacheProxy, sharedSpaceManager, includeCommunities, communitiesDataProvider);
            await genericUserProfileContextMenuController.ShowUserProfileContextMenuAsync(profile, position, offset, ct, closeMenuTask, onContextMenuHide, ConvertMenuAnchorPoint(anchorPoint), onContextMenuShow);
        }

        private async UniTask ShowCommunityPlayerEntryContextMenuAsync(Profile profile, Vector3 position, Vector2 offset, CancellationToken ct, Action onContextMenuHide,
            UniTask closeMenuTask, MenuAnchorPoint anchorPoint = MenuAnchorPoint.DEFAULT, bool isSpeaker = false)
        {
            communityPlayerEntryContextMenu ??= new CommunityPlayerEntryContextMenu(
                friendServiceProxy, chatEventBus, mvcManager,
                contextMenuSettings, analytics, onlineUsersProvider,
                realmNavigator, friendOnlineStatusCacheProxy, sharedSpaceManager,
                voiceChatContextMenuSettings, voiceChatOrchestrator, communitiesDataProvider);

            await communityPlayerEntryContextMenu.ShowUserProfileContextMenuAsync(profile, position, offset, ct, closeMenuTask, onContextMenuHide, ConvertMenuAnchorPoint(anchorPoint), isSpeaker);
        }


        public async UniTask OpenPassportAsync(string userId, CancellationToken ct = default)
        {
            try { await mvcManager.ShowAsync(PassportController.IssueCommand(new PassportParams(userId)), ct); }
            catch (Exception ex) { ReportHub.LogError(ReportCategory.UI, $"Failed to open passport for user {userId}: {ex.Message}"); }
        }
        public async UniTask ShowGenericContextMenuAsync(GenericContextMenuParameter parameter)
        {
            await mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(parameter));
        }

        private ContextMenuOpenDirection ConvertMenuAnchorPoint(MenuAnchorPoint anchorPoint)
        {
            switch (anchorPoint)
            {
                case MenuAnchorPoint.TOP_LEFT:
                    return ContextMenuOpenDirection.TOP_LEFT;
                case MenuAnchorPoint.TOP_RIGHT:
                    return ContextMenuOpenDirection.TOP_RIGHT;
                case MenuAnchorPoint.BOTTOM_LEFT:
                    return ContextMenuOpenDirection.BOTTOM_LEFT;
                case MenuAnchorPoint.BOTTOM_RIGHT:
                    return ContextMenuOpenDirection.BOTTOM_RIGHT;
                case MenuAnchorPoint.CENTER_LEFT:
                    return ContextMenuOpenDirection.CENTER_LEFT;
                case MenuAnchorPoint.CENTER_RIGHT:
                    return ContextMenuOpenDirection.CENTER_RIGHT;
                default:
                case MenuAnchorPoint.DEFAULT:
                    return ContextMenuOpenDirection.BOTTOM_RIGHT;
            }
        }
    }
}
