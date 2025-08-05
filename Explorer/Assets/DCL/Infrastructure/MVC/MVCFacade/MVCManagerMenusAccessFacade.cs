using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.Chat.EventBus;
using DCL.Communities;
using DCL.Communities.CommunitiesCard.Members;
using DCL.ExternalUrlPrompt;
using DCL.Friends;
using DCL.Multiplayer.Connectivity;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles;
using DCL.TeleportPrompt;
using DCL.UI;
using DCL.UI.GenericContextMenu.Controllers;
using DCL.UI.GenericContextMenuParameter;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.VoiceChat;
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
        private readonly ObjectProxy<FriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy;
        private readonly IProfileRepository profileRepository;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly CommunityVoiceChatContextMenuConfiguration voiceChatContextMenuSettings;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly CommunitiesDataProvider communityDataProvider;

        private CancellationTokenSource cancellationTokenSource;
        private GenericUserProfileContextMenuController genericUserProfileContextMenuController;
        private CommunityPlayerEntryContextMenu communityPlayerEntryContextMenu;
        private ChatOptionsContextMenuController chatOptionsContextMenuController;

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
            CommunitiesDataProvider communityDataProvider)
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
            this.communityDataProvider = communityDataProvider;
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
            Action onHide = null, MenuAnchorPoint anchorPoint = MenuAnchorPoint.DEFAULT)
        {
            Profile profile = await profileRepository.GetAsync(walletId, ct);

            if (profile == null)
                return;

            await ShowUserProfileContextMenuAsync(profile, position, offset, ct, onHide, closeMenuTask, anchorPoint);
        }

        public async UniTask ShowCommunityPlayerEntryContextMenuAsync(string participantWalletId, bool isSpeaker, bool isModeratorOrAdmin, Vector3 position, Vector2 offset, CancellationToken ct, UniTask closeMenuTask, Action onHide = null, MenuAnchorPoint anchorPoint = MenuAnchorPoint.DEFAULT)
        {
            if (string.IsNullOrEmpty(participantWalletId)) return;

            Web3Address walletId = new Web3Address(participantWalletId);
            Profile profile = await profileRepository.GetAsync(walletId, ct);

            if (profile == null) return;

            await ShowCommunityPlayerEntryContextMenu(profile, position, offset, ct, onHide, closeMenuTask, anchorPoint, isSpeaker, isModeratorOrAdmin);
        }

        public async UniTask ShowUserProfileContextMenuFromUserNameAsync(string userName, Vector3 position, Vector2 offset, CancellationToken ct, UniTask closeMenuTask,
            Action onHide = null)
        {
            Profile profile = profileCache.GetByUserName(userName);
            if (profile == null) return;
            await ShowUserProfileContextMenuAsync(profile, position, offset, ct, onHide, closeMenuTask);
        }

        public async UniTaskVoid ShowChatContextMenuAsync(Vector3 transformPosition, ChatOptionsContextMenuData data, Action onDeleteChatHistoryClicked, Action onContextMenuHide, UniTask closeMenuTask)
        {
            chatOptionsContextMenuController ??= new ChatOptionsContextMenuController(mvcManager, data.DeleteChatHistoryIcon, data.DeleteChatHistoryText, onDeleteChatHistoryClicked);
            await chatOptionsContextMenuController.ShowContextMenuAsync(transformPosition, closeMenuTask, onContextMenuHide);
        }

        private async UniTask ShowUserProfileContextMenuAsync(Profile profile, Vector3 position, Vector2 offset, CancellationToken ct, Action onContextMenuHide,
            UniTask closeMenuTask, MenuAnchorPoint anchorPoint = MenuAnchorPoint.DEFAULT)
        {
            genericUserProfileContextMenuController ??= new GenericUserProfileContextMenuController(friendServiceProxy, chatEventBus, mvcManager, contextMenuSettings, analytics, includeUserBlocking, onlineUsersProvider, realmNavigator, friendOnlineStatusCacheProxy, sharedSpaceManager);
            await genericUserProfileContextMenuController.ShowUserProfileContextMenuAsync(profile, position, offset, ct, closeMenuTask, onContextMenuHide, ConvertMenuAnchorPoint(anchorPoint));
        }

        private async UniTask ShowCommunityPlayerEntryContextMenu(Profile profile, Vector3 position, Vector2 offset, CancellationToken ct, Action onContextMenuHide,
            UniTask closeMenuTask, MenuAnchorPoint anchorPoint = MenuAnchorPoint.DEFAULT, bool isSpeaker = false, bool isModeratorOrAdmin = false)
        {
            communityPlayerEntryContextMenu ??= new CommunityPlayerEntryContextMenu(
                friendServiceProxy, chatEventBus, mvcManager,
                contextMenuSettings, analytics, onlineUsersProvider,
                realmNavigator, friendOnlineStatusCacheProxy, sharedSpaceManager,
                voiceChatContextMenuSettings, voiceChatOrchestrator, communityDataProvider);

            await communityPlayerEntryContextMenu.ShowUserProfileContextMenuAsync(profile, position, offset, ct, closeMenuTask, onContextMenuHide, ConvertMenuAnchorPoint(anchorPoint), isSpeaker, isModeratorOrAdmin);
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
