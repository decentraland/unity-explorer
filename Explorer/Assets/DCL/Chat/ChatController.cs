using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.CharacterCamera;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.EventBus;
using DCL.Diagnostics;
using DCL.Communities;
using DCL.Communities.CommunitiesCard;
using DCL.FeatureFlags;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.Prefs;
using DCL.Profiles;
using DCL.Profiles.Helpers;
using DCL.UI.Profiles.Helpers;
using DCL.RealmNavigation;
using DCL.Settings.Settings;
using DCL.UI;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenuParameter;
using DCL.UI.InputFieldFormatting;
using DCL.Web3.Identities;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using Utility;
using Utility.Arch;
using DCL.VoiceChat;
using DCL.Web3;
using Decentraland.SocialService.V2;
using ECS.Abstract;
using ECS.SceneLifeCycle.Realm;
using LiveKit.Rooms;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.InputSystem;
using Utility.Types;
using ChatMessage = DCL.Chat.History.ChatMessage;

namespace DCL.Chat
{
    public class ChatController : ControllerBase<ChatView, ChatControllerShowParams>,
        IControllerInSharedSpace<ChatView, ChatControllerShowParams>
    {
        public delegate void ConversationOpenedDelegate(bool wasAlreadyOpen);
        public delegate void ConversationClosedDelegate();

        private const string WELCOME_MESSAGE = "Type /help for available commands.";
        private const string NEW_CHAT_MESSAGE = "The chat starts here! Time to say hi! \\U0001F44B";
        private const string GET_COMMUNITY_FAILED_MESSAGE = "Unable to load new Community chat. Please restart Decentraland to try again.";
        private const string GET_USER_COMMUNITIES_FAILED_MESSAGE = "Unable to load Community chats. Please restart Decentraland to try again.";

        private readonly IChatMessagesBus chatMessagesBus;
        private readonly NametagsData nametagsData;
        private readonly IChatHistory chatHistory;
        private readonly World world;
        private readonly IInputBlock inputBlock;
        private readonly IRoom islandRoom;
        private readonly IProfileCache profileCache;
        private readonly ITextFormatter hyperlinkTextFormatter;
        private readonly ChatSettingsAsset chatSettings;
        private readonly IChatEventBus chatEventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ILoadingStatus loadingStatus;
        private readonly ChatHistoryStorage? chatStorage;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly ChatUserStateUpdater chatUserStateUpdater;
        private readonly IChatUserStateEventBus chatUserStateEventBus;
        private readonly ChatControllerChatBubblesHelper chatBubblesHelper;
        private readonly ChatControllerMemberListHelper memberListHelper;
        private readonly IRoomHub roomHub;
        private CallButtonController callButtonController;
        private CommunityStreamButtonController communityStreamButtonController;
        private CommunityStreamSubTitleBarController communityStreamSubTitleBarController;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly ISpriteCache thumbnailCache;
        private readonly IMVCManager mvcManager;
        private readonly WarningNotificationView warningNotificationView;
        private readonly CommunitiesEventBus communitiesEventBus;
        private readonly bool isCallEnabled;

        private readonly List<ChatUserData> membersBuffer = new ();
        private readonly List<ChatUserData> participantProfileBuffer = new ();
        private readonly Dictionary<ChatChannel.ChannelId, GetUserCommunitiesData.CommunityData> userCommunities = new ();
        private readonly UserConnectivityInfoProvider userConnectivityInfoProvider;

        private SingleInstanceEntity cameraEntity;

        // Used exclusively to calculate the new value of the read messages once the Unread messages separator has been viewed
        private int messageCountWhenSeparatorViewed;
        private bool hasToResetUnreadMessagesWhenNewMessageArrive;

        private string closedCommunityChatsKey;

        private bool viewInstanceCreated;
        private CancellationTokenSource chatUsersUpdateCts = new ();
        private CancellationTokenSource communitiesServiceCts = new ();
        private CancellationTokenSource errorNotificationCts = new ();
        private CancellationTokenSource memberListCts = new ();
        private CancellationTokenSource isUserAllowedInInitializationCts;
        private CancellationTokenSource isUserAllowedInCommunitiesBusSubscriptionCts;

        public string IslandRoomSid => islandRoom.Info.Sid;
        public string PreviousRoomSid { get; set; } = string.Empty;

        public ReactiveProperty<ChatChannel> CurrentChannel { get; } = new ReactiveProperty<ChatChannel>(ChatChannel.NEARBY_CHANNEL);

        public event ConversationOpenedDelegate? ConversationOpened;
        public event ConversationClosedDelegate? ConversationClosed;
        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        private readonly ObjectProxy<IFriendsService> friendsServiceProxy;

        private bool IsViewReady => viewInstanceCreated && viewInstance != null;

        public ChatController(
            ViewFactoryMethod viewFactory,
            IChatMessagesBus chatMessagesBus,
            IChatHistory chatHistory,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            NametagsData nametagsData,
            World world,
            Entity playerEntity,
            IInputBlock inputBlock,
            IRoomHub roomHub,
            ChatSettingsAsset chatSettings,
            ITextFormatter hyperlinkTextFormatter,
            IProfileCache profileCache,
            IChatEventBus chatEventBus,
            IWeb3IdentityCache web3IdentityCache,
            ILoadingStatus loadingStatus,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            RPCChatPrivacyService chatPrivacyService,
            IFriendsEventBus friendsEventBus,
            ChatHistoryStorage chatStorage,
            ObjectProxy<IFriendsService> friendsService,
            ProfileRepositoryWrapper profileDataProvider,
            CommunitiesDataProvider communitiesDataProvider,
            ISpriteCache thumbnailCache,
            IMVCManager mvcManager,
            WarningNotificationView warningNotificationView,
            CommunitiesEventBus communitiesEventBus,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            IRealmNavigator realmNavigator
            ) : base(viewFactory)
        {
            this.chatMessagesBus = chatMessagesBus;
            this.chatHistory = chatHistory;
            this.nametagsData = nametagsData;
            this.world = world;
            this.inputBlock = inputBlock;
            this.islandRoom = roomHub.IslandRoom();
            this.roomHub = roomHub;
            this.chatSettings = chatSettings;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
            this.profileCache = profileCache;
            this.chatEventBus = chatEventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.loadingStatus = loadingStatus;
            this.chatStorage = chatStorage;
            this.profileRepositoryWrapper = profileDataProvider;
            friendsServiceProxy = friendsService;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.communitiesDataProvider = communitiesDataProvider;
            this.thumbnailCache = thumbnailCache;
            this.mvcManager = mvcManager;
            this.warningNotificationView = warningNotificationView;
            this.communitiesEventBus = communitiesEventBus;

            this.isCallEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT);

            chatUserStateEventBus = new ChatUserStateEventBus();
            var chatRoom = roomHub.ChatRoom();

            chatUserStateUpdater = new ChatUserStateUpdater(
                userBlockingCacheProxy,
                chatRoom.Participants,
                chatSettings,
                chatPrivacyService,
                chatUserStateEventBus,
                friendsEventBus,
                chatRoom,
                friendsService);

            chatBubblesHelper = new ChatControllerChatBubblesHelper(
                world,
                playerEntity,
                entityParticipantTable,
                profileCache,
                nametagsData,
                chatSettings);

            memberListHelper = new ChatControllerMemberListHelper(
                roomHub,
                membersBuffer,
                GetChannelMembersAsync,
                participantProfileBuffer,
                this,
                chatHistory,
                userCommunities,
                communitiesDataProvider);

            userConnectivityInfoProvider = new UserConnectivityInfoProvider(roomHub.IslandRoom(), roomHub.ChatRoom(), communitiesEventBus, chatHistory, realmNavigator);
        }

#region Panel Visibility
        public bool IsVisibleInSharedSpace => State != ControllerState.ViewHidden && GetViewVisibility() && viewInstance!.IsUnfolded;

        /// <summary>
        /// The chat is considered Folded when its hidden either through the sidebar button or through the close button on the chat title bar.
        /// In this state it won't display any chat message, just the empty input box. And Unread messages will accumulate on the sidebar chat icon.
        /// </summary>
        public bool IsUnfolded
        {
            get => viewInstanceCreated && viewInstance!.IsUnfolded;

            set
            {
                if (!viewInstanceCreated) return;
                viewInstance!.IsUnfolded = value;

                // When opened from outside,
                // it should show the unread messages
                if (value)
                {
                    // Set input state to connected if we are in the NEARBY_CHANNEL_ID
                    // https://github.com/decentraland/unity-explorer/issues/4186
                    if (chatUserStateUpdater.CurrentConversation.Equals(ChatChannel.NEARBY_CHANNEL_ID.Id))
                    {
                        SetupViewWithUserStateOnMainThreadAsync(ChatUserStateUpdater.ChatUserState.CONNECTED).Forget();
                        return;
                    }
                    else if (chatHistory.Channels[viewInstance.CurrentChannelId].ChannelType == ChatChannel.ChatChannelType.USER)
                    {
                        chatUsersUpdateCts = chatUsersUpdateCts.SafeRestart();
                        UpdateChatUserStateAsync(chatUserStateUpdater.CurrentConversation, true, chatUsersUpdateCts.Token).Forget();
                    }

                    viewInstance.ShowNewMessages();
                }
            }
        }

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ChatControllerShowParams showParams)
        {
            if (State != ControllerState.ViewHidden)
            {
                if (!viewInstanceCreated)
                    return;

                if (!GetViewVisibility())
                    SetViewVisibility(true);

                if (showParams.Unfold)
                    IsUnfolded = true;

                if (showParams.Focus)
                    viewInstance!.Focus();

                ViewShowingComplete?.Invoke(this);
            }

            await UniTask.CompletedTask;
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            IsUnfolded = false;
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// Makes the chat panel (including the input box) invisible or visible (it does not hide the view, it disables the GameObject).
        /// </summary>
        /// <param name="visibility">Whether to make the panel visible.</param>
        public void SetViewVisibility(bool visibility)
        {
            if (viewInstanceCreated)
                viewInstance!.gameObject.SetActive(visibility);
        }

        /// <summary>
        /// Indicates whether the panel is active or not (the view is never really hidden as it is a Persistent panel).
        /// </summary>
        private bool GetViewVisibility() =>
            viewInstanceCreated && viewInstance != null && viewInstance.gameObject.activeInHierarchy;
#endregion

#region View Show and Close
        protected override void OnViewShow()
        {
            cameraEntity = world.CacheCamera();

            viewInstance.SetProfileDataPovider(profileRepositoryWrapper);

            viewInstance.Initialize(chatHistory.Channels, chatSettings, GetChannelMembersAsync, loadingStatus, profileCache, thumbnailCache, OpenContextMenuAsync);

            callButtonController = new CallButtonController(viewInstance.chatTitleBar.CallButton, voiceChatOrchestrator, chatEventBus);
            viewInstance.chatTitleBar.CallButton.gameObject.SetActive(isCallEnabled);

            communityStreamButtonController = new CommunityStreamButtonController(
                viewInstance.chatTitleBar.CommunitiesCallButton,
                voiceChatOrchestrator,
                chatEventBus,
                CurrentChannel,
                communitiesDataProvider);

            communityStreamSubTitleBarController = new CommunityStreamSubTitleBarController(
                viewInstance.CommunityStreamSubTitleBar,
                voiceChatOrchestrator,
                CurrentChannel,
                communitiesDataProvider);

            viewInstance.chatTitleBar.CommunitiesCallButton.gameObject.SetActive(false);
            chatStorage?.SetNewLocalUserWalletAddress(web3IdentityCache.Identity!.Address);

            SubscribeToEvents();

            AddNearbyChannelAndSendWelcomeMessage();

            memberListHelper.StartUpdating();

            IsUnfolded = inputData.Unfold;
            viewInstance.Blur();

            InitializeChannelsAndConversationsAsync().Forget();
        }

        private async void OpenContextMenuAsync(GenericContextMenuParameter parameter, Action onClosed, CancellationToken ct)
        {
            await mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(parameter), ct);
            onClosed();
        }

        private void AddNearbyChannelAndSendWelcomeMessage()
        {
            var channel = chatHistory.AddOrGetChannel(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);
            viewInstance!.CurrentChannelId = ChatChannel.NEARBY_CHANNEL_ID;
            CurrentChannel.UpdateValue(channel);
            chatHistory.AddMessage(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY, ChatMessage.NewFromSystem(WELCOME_MESSAGE));
            chatHistory.Channels[ChatChannel.NEARBY_CHANNEL_ID].MarkAllMessagesAsRead();
        }

        private async UniTaskVoid InitializeChannelsAndConversationsAsync()
        {
            //We need the friends service enabled to be able to interact with them via chat.
            //If there is no friends service (like in LSD) these two methods should not be invoked
            if (friendsServiceProxy.Configured)
            {
                if (chatStorage != null)
                    chatStorage.LoadAllChannelsWithoutMessages();

                var connectedUsers = await chatUserStateUpdater.InitializeAsync(chatHistory.Channels.Keys);

                await UniTask.SwitchToMainThread();
                viewInstance!.SetupInitialConversationToolbarStatusIconForUsers(connectedUsers);
            }

            isUserAllowedInInitializationCts = isUserAllowedInInitializationCts.SafeRestart();
            bool isCommunityEnabled = await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(isUserAllowedInInitializationCts.Token);

            if (isCommunityEnabled)
                await InitializeCommunityConversationsAsync();

            userConnectivityInfoProvider.Initialize(isCommunityEnabled, communitiesDataProvider);
        }

        protected override void OnViewClose()
        {
            Blur();
            UnsubscribeFromEvents();
            Dispose();
            callButtonController.Reset();
            communityStreamButtonController?.Reset();
            communityStreamSubTitleBarController?.Dispose();
        }
#endregion

#region Communities

        private async UniTask InitializeCommunityConversationsAsync()
        {
            if (string.IsNullOrEmpty(closedCommunityChatsKey))
                closedCommunityChatsKey = string.Format(DCLPrefKeys.CLOSED_COMMUNITY_CHATS, web3IdentityCache.Identity!.Address.ToString().ToLower());

            // Obtains all the communities of the user
            const int ALL_COMMUNITIES_OF_USER = 100;
            communitiesServiceCts = communitiesServiceCts.SafeRestart();
            Result<GetUserCommunitiesResponse> result = await communitiesDataProvider.GetUserCommunitiesAsync(string.Empty, true, 0, ALL_COMMUNITIES_OF_USER, communitiesServiceCts.Token).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (communitiesServiceCts.IsCancellationRequested)
                return;

            if (result.Success)
            {
                await UniTask.SwitchToMainThread();

                // Puts the results into a dictionary
                userCommunities.Clear();
                GetUserCommunitiesResponse response = result.Value;

                for (int i = 0; i < response.data.results.Length; ++i)
                {
                    if (!IsCommunityChatClosed(response.data.results[i].id))
                        userCommunities.Add(ChatChannel.NewCommunityChannelId(response.data.results[i].id), response.data.results[i]);
                }

                // Gives the data to the view so it can fill the items UI when new conversations are added
                viewInstance!.SetCommunitiesData(userCommunities);

                // Creates one channel per community
                for (int i = 0; i < response.data.results.Length; ++i)
                {
                    if (!IsCommunityChatClosed(response.data.results[i].id))
                        chatHistory.AddOrGetChannel(ChatChannel.NewCommunityChannelId(response.data.results[i].id), ChatChannel.ChatChannelType.COMMUNITY);
                }
            }
            else
            {
                ReportHub.LogError(ReportCategory.COMMUNITIES, GET_USER_COMMUNITIES_FAILED_MESSAGE + result.ErrorMessage ?? string.Empty);
                ShowErrorNotificationAsync(GET_USER_COMMUNITIES_FAILED_MESSAGE, errorNotificationCts.Token).Forget();
            }
        }

        private async UniTask AddCommunityConversationAsync(string communityId, bool setAsCurrentChannel = false)
        {
            communitiesServiceCts = communitiesServiceCts.SafeRestart();
            Result<GetCommunityResponse> result = await communitiesDataProvider.GetCommunityAsync(communityId, communitiesServiceCts.Token).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (communitiesServiceCts.IsCancellationRequested)
                return;

            if (result.Success)
            {
                await UniTask.SwitchToMainThread();

                GetCommunityResponse response = result.Value;

                var channelId = ChatChannel.NewCommunityChannelId(response.data.id);
                userCommunities.Add(channelId, new GetUserCommunitiesData.CommunityData()
                    {
                        id = response.data.id,
                        thumbnails = response.data.thumbnails,
                        name = response.data.name,
                        privacy = response.data.privacy,
                        role = response.data.role,
                        ownerAddress = response.data.ownerAddress
                    });

                viewInstance!.SetCommunitiesData(userCommunities);

                var channel = chatHistory.AddOrGetChannel(ChatChannel.NewCommunityChannelId(response.data.id), ChatChannel.ChatChannelType.COMMUNITY);

                if (setAsCurrentChannel)
                {
                    CurrentChannel.UpdateValue(channel);
                    viewInstance!.CurrentChannelId = channelId;
                }
            }
            else
            {
                ReportHub.LogError(ReportCategory.COMMUNITIES, GET_COMMUNITY_FAILED_MESSAGE + result.ErrorMessage ?? string.Empty);
                ShowErrorNotificationAsync(GET_COMMUNITY_FAILED_MESSAGE, errorNotificationCts.Token).Forget();
            }
        }

#endregion

#region Other Controller-inherited Methods
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            memberListHelper.SetView(viewInstance!);
            viewInstanceCreated = true;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }

        public override void Dispose()
        {
            userConnectivityInfoProvider.Dispose();
            viewInstance?.RemoveAllConversations();
            viewInstance?.Dispose();
            chatStorage?.UnloadAllFiles();
            chatUserStateUpdater.Dispose();
            chatHistory.DeleteAllChannels();
            memberListHelper.Dispose();
            chatUsersUpdateCts.SafeCancelAndDispose();
            callButtonController?.Dispose();
            communityStreamButtonController?.Dispose();
            communitiesServiceCts.SafeCancelAndDispose();
            errorNotificationCts.SafeCancelAndDispose();
            memberListCts.SafeCancelAndDispose();
            isUserAllowedInInitializationCts.SafeCancelAndDispose();
            isUserAllowedInCommunitiesBusSubscriptionCts.SafeCancelAndDispose();
        }
#endregion

#region Conversation Events
        private void OnOpenPrivateConversationRequested(string userId)
        {
            ChatChannel.ChannelId channelId = new ChatChannel.ChannelId(userId);
            ConversationOpened?.Invoke(chatHistory.Channels.ContainsKey(channelId));

            var channel = chatHistory.AddOrGetChannel(channelId, ChatChannel.ChatChannelType.USER);
            chatUserStateUpdater.CurrentConversation = userId;
            chatUserStateUpdater.AddConversation(userId);

            CurrentChannel.UpdateValue(channel);
            viewInstance!.CurrentChannelId = channelId;

            chatUsersUpdateCts = chatUsersUpdateCts.SafeRestart();
            UpdateChatUserStateAsync(userId, true, chatUsersUpdateCts.Token).Forget();

            viewInstance.Focus();
        }

        private void OnStartCall(string userId)
        {
            voiceChatOrchestrator.StartCall(new Web3Address(userId), VoiceChatType.PRIVATE);
        }

        private void OnCommunitiesDataProviderCommunityCreated(CreateOrUpdateCommunityResponse.CommunityData newCommunity)
        {
            ChatChannel.ChannelId channelId = ChatChannel.NewCommunityChannelId(newCommunity.id);

            userCommunities[channelId] = new GetUserCommunitiesData.CommunityData()
            {
                id = newCommunity.id,
                thumbnails = newCommunity.thumbnails,
                description = newCommunity.description,
                ownerAddress = newCommunity.ownerAddress,
                name = newCommunity.name,
                privacy = newCommunity.privacy,
                role = CommunityMemberRole.owner,
                membersCount = 1
            };

            chatHistory.AddOrGetChannel(channelId, ChatChannel.ChatChannelType.COMMUNITY);
        }

        private void OnCommunitiesDataProviderCommunityDeleted(string communityId)
        {
            ChatChannel.ChannelId channelId = ChatChannel.NewCommunityChannelId(communityId);
            chatHistory.RemoveChannel(channelId);
        }

        private void OnSelectConversation(ChatChannel.ChannelId channelId)
        {
            if (!IsViewReady)
                return;

            if (chatHistory.Channels[channelId].ChannelType == ChatChannel.ChatChannelType.USER)
                chatUserStateUpdater.CurrentConversation = channelId.Id;

            CurrentChannel.UpdateValue(chatHistory.Channels[channelId]);
            viewInstance!.CurrentChannelId = channelId;

            if (chatHistory.Channels[channelId].ChannelType == ChatChannel.ChatChannelType.USER)
            {
                chatUserStateUpdater.AddConversation(channelId.Id);
                chatUsersUpdateCts = chatUsersUpdateCts.SafeRestart();
                UpdateChatUserStateAsync(channelId.Id, true, chatUsersUpdateCts.Token).Forget();
            }
            else
                SetupViewWithUserStateOnMainThreadAsync(ChatUserStateUpdater.ChatUserState.CONNECTED).Forget();
        }

        private async UniTaskVoid UpdateChatUserStateAsync(string userId, bool updateToolbar, CancellationToken ct)
        {
            if (!IsViewReady)
                return;

            Result<ChatUserStateUpdater.ChatUserState> result = await chatUserStateUpdater.GetChatUserStateAsync(userId, ct).SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);

            if (ct.IsCancellationRequested)
                return;

            if (result.Success == false)
                return;

            ChatUserStateUpdater.ChatUserState userState = result.Value;

            SetupViewWithUserStateOnMainThreadAsync(userState).Forget();
            UpdateCallButtonUserState(userState, userId);

            if (!updateToolbar)
                return;

            bool offline = userState is ChatUserStateUpdater.ChatUserState.DISCONNECTED or ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER;
            viewInstance.UpdateConversationStatusIconForUser(userId, offline ? OnlineStatus.OFFLINE : OnlineStatus.ONLINE);
        }

        private void UpdateCallButtonUserState(ChatUserStateUpdater.ChatUserState userState, string userId)
        {
            if (!isCallEnabled) return;

            CallButtonController.OtherUserCallStatus callStatus = CallButtonController.OtherUserCallStatus.USER_OFFLINE;

            switch (userState)
            {
                case ChatUserStateUpdater.ChatUserState.CONNECTED:
                    callStatus = CallButtonController.OtherUserCallStatus.USER_AVAILABLE;
                    break;
                case ChatUserStateUpdater.ChatUserState.DISCONNECTED:
                    callStatus = CallButtonController.OtherUserCallStatus.USER_OFFLINE;
                    break;
                case ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED:
                    callStatus = CallButtonController.OtherUserCallStatus.USER_REJECTS_CALLS;
                    break;
                case ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER:
                    callStatus = CallButtonController.OtherUserCallStatus.OWN_USER_REJECTS_CALLS;
                    break;
            }

            callButtonController.SetCallStatusForUser(callStatus, userId);
        }
#endregion

#region Chat History Events
        private void OnChatHistoryMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            bool isSentByOwnUser = addedMessage is { IsSystemMessage: false, IsSentByOwnUser: true };

            string? communityName = destinationChannel.ChannelType == ChatChannel.ChatChannelType.COMMUNITY ? userCommunities[destinationChannel.Id].name : null;

            chatBubblesHelper.CreateChatBubble(destinationChannel, addedMessage, isSentByOwnUser, communityName);

            if (isSentByOwnUser)
            {
                MarkCurrentChannelAsRead();

                if (IsViewReady)
                {
                    viewInstance!.RefreshMessages();
                    viewInstance.ShowLastMessage();
                }
            }
            else
            {
                HandleMessageAudioFeedback(addedMessage);

                if (IsViewReady)
                {
                    bool shouldMarkChannelAsRead = viewInstance is { IsMessageListVisible: true, IsScrollAtBottom: true };
                    bool isCurrentChannel = destinationChannel.Id.Equals(viewInstance!.CurrentChannelId);

                    if (isCurrentChannel)
                    {
                        if (shouldMarkChannelAsRead)
                            MarkCurrentChannelAsRead();

                        HandleUnreadMessagesSeparator(destinationChannel);
                        viewInstance.RefreshMessages();
                    }
                    else
                        viewInstance.RefreshUnreadMessages(destinationChannel.Id);
                }
            }

            // Moves the conversation icon to the top, beneath nearby
            if (destinationChannel.ChannelType != ChatChannel.ChatChannelType.NEARBY)
                viewInstance?.MoveChannelToTop(destinationChannel.Id);
        }

        private void HandleMessageAudioFeedback(ChatMessage message)
        {
            if (IsViewReady)
                return;

            switch (chatSettings.chatAudioSettings)
            {
                case ChatAudioSettings.NONE:
                    return;
                case ChatAudioSettings.MENTIONS_ONLY when message.IsMention:
                case ChatAudioSettings.ALL:
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(message.IsMention ? viewInstance!.ChatReceiveMentionMessageAudio : viewInstance!.ChatReceiveMessageAudio);
                    break;
            }
        }

        private void HandleUnreadMessagesSeparator(ChatChannel channel)
        {
            if (!hasToResetUnreadMessagesWhenNewMessageArrive)
                return;

            hasToResetUnreadMessagesWhenNewMessageArrive = false;
            channel.ReadMessages = messageCountWhenSeparatorViewed;
        }

        private void OnChatHistoryReadMessagesChanged(ChatChannel changedChannel)
        {
            if (changedChannel.Id.Equals(viewInstance!.CurrentChannelId))
                viewInstance!.RefreshMessages();
            else
                viewInstance.RefreshUnreadMessages(changedChannel.Id);
        }
#endregion

#region Channel Events

        private async void OnViewCurrentChannelChangedAsync()
        {
            if (chatHistory.Channels[viewInstance!.CurrentChannelId].ChannelType == ChatChannel.ChatChannelType.USER &&
                chatStorage != null && !chatStorage.IsChannelInitialized(viewInstance.CurrentChannelId))
            {
                await chatStorage.InitializeChannelWithMessagesAsync(viewInstance.CurrentChannelId);
                chatHistory.Channels[viewInstance.CurrentChannelId].MarkAllMessagesAsRead();

                if (chatHistory.Channels[viewInstance.CurrentChannelId].Messages.Count == 0)
                    chatHistory.AddMessage(viewInstance.CurrentChannelId, chatHistory.Channels[viewInstance.CurrentChannelId].ChannelType, ChatMessage.NewFromSystem(NEW_CHAT_MESSAGE));

                viewInstance.RefreshMessages();
            }

            // Note: The check is necessary because when the chat loads the Nearby participant list is not ready yet
            if(userConnectivityInfoProvider.HasConversation(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY))
                viewInstance.SetOnlineUserAddresses(userConnectivityInfoProvider.GetOnlineUsersInConversation(viewInstance!.CurrentChannelId, chatHistory.Channels[viewInstance!.CurrentChannelId].ChannelType));
        }

        private void OnViewChannelRemovalRequested(ChatChannel.ChannelId channelId)
        {
            ConversationClosed?.Invoke();

            chatHistory.RemoveChannel(channelId);
        }
#endregion

#region View state changes event handling
        // This is called when the view is folded or unfolded
        // it will mark the current channel as read if it is folded
        private void OnViewFoldingChanged(bool isUnfolded)
        {
            if (!isUnfolded)
                MarkCurrentChannelAsRead();
        }

        private void OnViewUnreadMessagesSeparatorViewed()
        {
            messageCountWhenSeparatorViewed = chatHistory.Channels[viewInstance!.CurrentChannelId].Messages.Count;
            hasToResetUnreadMessagesWhenNewMessageArrive = true;
        }

        private void OnViewInputSubmitted(ChatChannel channel, string message, string origin)
        {
            chatMessagesBus.Send(channel, message, origin);
        }

        private void OnViewEmojiSelectionVisibilityChanged(bool isVisible)
        {
            if (isVisible)
                DisableUnwantedInputs();
            else
                EnableUnwantedInputs();
        }

        private void OnViewFocusChanged(bool isFocused)
        {
            callButtonController.Reset();

            if (isFocused)
                DisableUnwantedInputs();
            else
                EnableUnwantedInputs();
        }

        private void OnViewPointerExit()
        {
            world.TryRemove<CameraBlockerComponent>(cameraEntity);
        }

        private void OnViewPointerEnter()
        {
            world.AddOrGet(cameraEntity, new CameraBlockerComponent());
        }

        private void OnViewScrollBottomReached()
        {
            MarkCurrentChannelAsRead();
        }

        private void OnViewMemberListVisibilityChanged(bool isVisible)
        {
            communityStreamSubTitleBarController?.OnMemberListVisibilityChanged(isVisible);

            if (isVisible && roomHub.HasAnyRoomConnected())
                RefreshMemberList();
        }

        private void RefreshMemberList()
        {
            memberListCts = memberListCts.SafeRestart();
            memberListHelper.RefreshMemberListAsync(memberListCts.Token).Forget();
        }
#endregion

#region External components event handling
        private void OnOpenChatCommandLineShortcutPerformed(InputAction.CallbackContext obj)
        {
            // NOTE: it's wired in the ChatPlugin
            //TODO FRAN: This should take us to the nearby channel and send the command there
            viewInstance!.Focus("/");
        }

        //This comes from the paste option or mention, we check if it's possible to do it as if there is a mask we cannot
        private void OnTextInserted(string text)
        {
            if (viewInstance!.IsMaskActive) return;

            viewInstance.Focus();
            viewInstance.InsertTextInInputBox(text);
        }

        private void OnToggleNametagsShortcutPerformed(InputAction.CallbackContext obj)
        {
            nametagsData.showNameTags = !nametagsData.showNameTags;
        }

        private void OnChatBusMessageAdded(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType, ChatMessage chatMessage)
        {
            if (channelType == ChatChannel.ChatChannelType.COMMUNITY && !userCommunities.ContainsKey(channelId))
                return;

            if (!chatMessage.IsSystemMessage)
            {
                string formattedText = hyperlinkTextFormatter.FormatText(chatMessage.Message);
                var newChatMessage = ChatMessage.CopyWithNewMessage(formattedText, chatMessage);
                chatHistory.AddMessage(channelId, channelType, newChatMessage);
            }
            else
                chatHistory.AddMessage(channelId, channelType, chatMessage);
        }
#endregion

#region Chat History Channel Events
        private void OnChatHistoryChannelRemoved(ChatChannel.ChannelId removedChannel, ChatChannel.ChatChannelType channelType)
        {
            if (channelType == ChatChannel.ChatChannelType.USER)
                chatUserStateUpdater.RemoveConversation(removedChannel.Id);
            else if (channelType == ChatChannel.ChatChannelType.COMMUNITY)
                CloseCommunityChat(userCommunities[removedChannel].id);

            userConnectivityInfoProvider.RemoveConversation(removedChannel, channelType);
            viewInstance!.RemoveConversation(removedChannel);
        }

        private void OnChatHistoryChannelAdded(ChatChannel addedChannel)
        {
            switch (addedChannel.ChannelType)
            {
                case ChatChannel.ChatChannelType.NEARBY:
                    viewInstance!.AddNearbyConversation(addedChannel);
                    break;
                case ChatChannel.ChatChannelType.COMMUNITY:
                    if (userCommunities.ContainsKey(addedChannel.Id))
                        viewInstance!.AddCommunityConversation(addedChannel, thumbnailCache);
                    break;
                case ChatChannel.ChatChannelType.USER:
                    chatUserStateUpdater.AddConversation(addedChannel.Id.Id);
                    viewInstance!.AddPrivateConversation(addedChannel);
                    break;
            }

            userConnectivityInfoProvider.AddConversation(addedChannel.Id, addedChannel.ChannelType);
        }
#endregion

#region User State Update Events
        /// <summary>
        /// NOTE: this event is raised when a user disconnects but belongs to the list
        /// NOTE: of opened conversations
        /// </summary>
        /// <param name="userId"></param>
        private void OnUserDisconnected(string userId)
        {
            // Update the state of the user in the current conversation
            // NOTE: if it's in the unfolded state (prevent setting the state of the chat input box if user is offline)
            if (!viewInstance!.IsUnfolded || chatHistory.Channels[viewInstance.CurrentChannelId].ChannelType != ChatChannel.ChatChannelType.USER)
                return;

            var state = chatUserStateUpdater.GetDisconnectedUserState(userId);
            SetupViewWithUserStateOnMainThreadAsync(state).Forget();
            UpdateCallButtonUserState(state, userId);
        }

        private void OnNonFriendConnected(string userId)
        {
            GetAndSetupNonFriendUserStateAsync(userId).Forget();
        }

        private async UniTaskVoid GetAndSetupNonFriendUserStateAsync(string userId)
        {
            //We might need a new state of type "LOADING" or similar to display until we resolve the real state
            SetupViewWithUserStateOnMainThreadAsync(ChatUserStateUpdater.ChatUserState.DISCONNECTED).Forget();
            var state = await chatUserStateUpdater.GetConnectedNonFriendUserStateAsync(userId);
            SetupViewWithUserStateOnMainThreadAsync(state).Forget();
            UpdateCallButtonUserState(state, userId);
        }

        private void OnFriendConnected(string userId)
        {
            var state = ChatUserStateUpdater.ChatUserState.CONNECTED;
            SetupViewWithUserStateOnMainThreadAsync(state).Forget();
            UpdateCallButtonUserState(state, userId);
        }

        private void OnUserBlockedByOwnUser(string userId)
        {
            var state = ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER;
            SetupViewWithUserStateOnMainThreadAsync(state).Forget();
        }

        private void OnCurrentConversationUserUnavailable()
        {
            var state = ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED;
            SetupViewWithUserStateOnMainThreadAsync(state).Forget();
            UpdateCallButtonUserState(state, viewInstance!.CurrentChannelId.Id);
        }

        private void OnCurrentConversationUserAvailable()
        {
            var state = ChatUserStateUpdater.ChatUserState.CONNECTED;
            SetupViewWithUserStateOnMainThreadAsync(state).Forget();
            UpdateCallButtonUserState(state, viewInstance!.CurrentChannelId.Id);
        }

        private void OnUserConnectionStateChanged(string userId, bool isConnected)
        {
            viewInstance!.UpdateConversationStatusIconForUser(userId, isConnected ? OnlineStatus.ONLINE : OnlineStatus.OFFLINE);
        }
#endregion

        private async UniTaskVoid SetupViewWithUserStateOnMainThreadAsync(ChatUserStateUpdater.ChatUserState userState)
        {
            await UniTask.SwitchToMainThread();
            viewInstance!.SetupViewWithUserState(userState);
        }

        private void MarkCurrentChannelAsRead()
        {
            chatHistory.Channels[viewInstance!.CurrentChannelId].MarkAllMessagesAsRead();
            messageCountWhenSeparatorViewed = chatHistory.Channels[viewInstance.CurrentChannelId].ReadMessages;
        }

        private void DisableUnwantedInputs()
        {
            world.AddOrGet(cameraEntity, new CameraBlockerComponent());
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
        }

        private void EnableUnwantedInputs()
        {
            world.TryRemove<CameraBlockerComponent>(cameraEntity);
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
        }

        private async UniTask GetChannelMembersAsync(List<ChatUserData> outMembers, CancellationToken ct)
        {
            ChatChannel.ChannelId channelId = viewInstance!.CurrentChannelId;

            outMembers.Clear();

            if (chatHistory.Channels[channelId].ChannelType == ChatChannel.ChatChannelType.NEARBY)
            {
                foreach (string? identity in roomHub.AllLocalRoomsRemoteParticipantIdentities())
                {
                    if (ct.IsCancellationRequested)
                        break;

                    // TODO: Use new endpoint to get a bunch of profile info
                    if (profileCache.TryGet(identity, out var profile))
                        outMembers.Add(new ChatUserData()
                        {
                            WalletAddress = profile.UserId,
                            FaceSnapshotUrl = profile.Avatar.FaceSnapshotUrl,
                            Name = profile.ValidatedName,
                            ConnectionStatus = ChatMemberConnectionStatus.Online,
                            ProfileColor = ProfileNameColorHelper.GetNameColor(profile.ValidatedName),
                            WalletId = profile.WalletId
                        });
                }
            }
            else if (chatHistory.Channels[channelId].ChannelType == ChatChannel.ChatChannelType.COMMUNITY)
            {
                Result<GetCommunityMembersResponse> result = await communitiesDataProvider.GetOnlineCommunityMembersAsync(userCommunities[channelId].id, ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (result.Success)
                {
                    GetCommunityMembersResponse response = result.Value;

                    foreach (GetCommunityMembersResponse.MemberData memberData in response.data.results)
                    {
                        // Skips the user of the player
                        if (memberData.memberAddress == web3IdentityCache.Identity.Address)
                            continue;

                        outMembers.Add(new ChatUserData()
                        {
                            WalletAddress = memberData.memberAddress,
                            FaceSnapshotUrl = memberData.profilePictureUrl,
                            Name = memberData.name,
                            ConnectionStatus = ChatMemberConnectionStatus.Online,
                            ProfileColor = ProfileNameColorHelper.GetNameColor(memberData.name),
                            WalletId = memberData.memberAddress = $"#{memberData.memberAddress[^4..]}"
                        });
                    }
                }
                else
                {
                    // TODO
                }
            }
        }

        /// <summary>
        /// When we press close button on the chat panel
        /// (close the chat - only the input box will remain visible)
        /// NOTE: this is the same behaviour as when we click the sidebar chat icon
        /// NOTE: toggle to close the chat panel
        /// </summary>
        private void OnCloseButtonClicked()
        {
            IsUnfolded = false;
        }

        /// <summary>
        /// When we click the input chat
        /// at the bottom of the chat panel (open the chat)
        /// NOTE: this is the same behaviour as when we click the sidebar chat icon
        /// NOTE: toggle to open the chat panel
        /// </summary>
        private void OnInputButtonClicked()
        {
            IsUnfolded = true;
        }

        private void SubscribeToEvents()
        {
            //We start processing messages once the view is ready
            chatMessagesBus.MessageAdded += OnChatBusMessageAdded;
            callButtonController.StartCall += OnStartCall;

            chatEventBus.InsertTextInChatRequested += OnTextInserted;
            chatEventBus.OpenPrivateConversationRequested += OnOpenPrivateConversationRequested;
            chatEventBus.OpenCommunityConversationRequested += OpenCommunityChat;

            viewInstance.OnCloseButtonClicked += OnCloseButtonClicked;
            viewInstance.OnInputButtonClicked += OnInputButtonClicked;
            viewInstance!.PointerEnter += OnViewPointerEnter;
            viewInstance.PointerExit += OnViewPointerExit;
            viewInstance.FocusChanged += OnViewFocusChanged;
            viewInstance.EmojiSelectionVisibilityChanged += OnViewEmojiSelectionVisibilityChanged;
            viewInstance.InputSubmitted += OnViewInputSubmitted;
            viewInstance.MemberListVisibilityChanged += OnViewMemberListVisibilityChanged;
            viewInstance.ScrollBottomReached += OnViewScrollBottomReached;
            viewInstance.UnreadMessagesSeparatorViewed += OnViewUnreadMessagesSeparatorViewed;
            viewInstance.FoldingChanged += OnViewFoldingChanged;
            viewInstance.ChannelRemovalRequested += OnViewChannelRemovalRequested;
            viewInstance.CurrentChannelChanged += OnViewCurrentChannelChangedAsync;
            viewInstance.ConversationSelected += OnSelectConversation;
            viewInstance.DeleteChatHistoryRequested += OnViewDeleteChatHistoryRequested;
            viewInstance.ViewCommunityRequested += OnViewViewCommunityRequested;

            chatHistory.ChannelAdded += OnChatHistoryChannelAdded;
            chatHistory.ChannelRemoved += OnChatHistoryChannelRemoved;
            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;
            chatHistory.MessageAdded += OnChatHistoryMessageAdded; // TODO: This should not exist, the only way to add a chat message from outside should be by using the bus
            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;

            chatUserStateEventBus.FriendConnected += OnFriendConnected;
            chatUserStateEventBus.UserDisconnected += OnUserDisconnected;
            chatUserStateEventBus.NonFriendConnected += OnNonFriendConnected;
            chatUserStateEventBus.CurrentConversationUserAvailable += OnCurrentConversationUserAvailable;
            chatUserStateEventBus.CurrentConversationUserUnavailable += OnCurrentConversationUserUnavailable;
            chatUserStateEventBus.UserBlocked += OnUserBlockedByOwnUser;
            chatUserStateEventBus.UserConnectionStateChanged += OnUserConnectionStateChanged;

            DCLInput.Instance.Shortcuts.ToggleNametags.performed += OnToggleNametagsShortcutPerformed;
            DCLInput.Instance.Shortcuts.OpenChatCommandLine.performed += OnOpenChatCommandLineShortcutPerformed;

            communitiesDataProvider.CommunityCreated += OnCommunitiesDataProviderCommunityCreated;
            communitiesDataProvider.CommunityDeleted += OnCommunitiesDataProviderCommunityDeleted;

            userConnectivityInfoProvider.UserConnected += OnUserConnectivityInfoProviderUserConnected;
            userConnectivityInfoProvider.UserDisconnected += OnUserConnectivityInfoProviderUserDisconnected;
            userConnectivityInfoProvider.ConversationInitialized += OnUserConnectivityInfoProviderConversationInitialized;

            SubscribeToCommunitiesBusEventsAsync().Forget();
        }

        private void OnUserConnectivityInfoProviderConversationInitialized(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType)
        {
            if(channelType is ChatChannel.ChatChannelType.NEARBY or ChatChannel.ChatChannelType.COMMUNITY &&
               viewInstance!.CurrentChannelId.Equals(channelId))
                viewInstance!.SetOnlineUserAddresses(userConnectivityInfoProvider.GetOnlineUsersInConversation(channelId, channelType));
        }

        private async UniTaskVoid SubscribeToCommunitiesBusEventsAsync()
        {
            isUserAllowedInCommunitiesBusSubscriptionCts = isUserAllowedInCommunitiesBusSubscriptionCts.SafeRestart();

            if (await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(isUserAllowedInCommunitiesBusSubscriptionCts.Token))
                communitiesEventBus.UserDisconnectedFromCommunity += OnCommunitiesEventBusUserDisconnectedToCommunity;
        }

        private void OnCommunitiesEventBusUserDisconnectedToCommunity(CommunityMemberConnectivityUpdate userConnectivity)
        {
            if (userConnectivity.Member.Address == web3IdentityCache.Identity!.Address)
                chatHistory.RemoveChannel(ChatChannel.NewCommunityChannelId(userConnectivity.CommunityId));
        }

        private void OnViewViewCommunityRequested(string communityId)
        {
            viewInstance!.Blur();
            mvcManager.ShowAsync(CommunityCardController.IssueCommand(new CommunityCardParameter(communityId)));
        }

        private void OnViewDeleteChatHistoryRequested()
        {
            // Clears the history of the current conversation and updates the UI
            chatHistory.ClearChannel(viewInstance!.CurrentChannelId);
            messageCountWhenSeparatorViewed = 0;
            viewInstance.ClearCurrentConversation();
        }

        private void UnsubscribeFromEvents()
        {
            chatMessagesBus.MessageAdded -= OnChatBusMessageAdded;
            chatHistory.MessageAdded -= OnChatHistoryMessageAdded;
            chatHistory.ReadMessagesChanged -= OnChatHistoryReadMessagesChanged;
            callButtonController.StartCall -= OnStartCall;
            chatEventBus.InsertTextInChatRequested -= OnTextInserted;

            if (viewInstance != null)
            {
                viewInstance.OnCloseButtonClicked -= OnCloseButtonClicked;
                viewInstance.OnInputButtonClicked -= OnInputButtonClicked;
                viewInstance.PointerEnter -= OnViewPointerEnter;
                viewInstance.PointerExit -= OnViewPointerExit;
                viewInstance.FocusChanged -= OnViewFocusChanged;
                viewInstance.EmojiSelectionVisibilityChanged -= OnViewEmojiSelectionVisibilityChanged;
                viewInstance.InputSubmitted -= OnViewInputSubmitted;
                viewInstance.ScrollBottomReached -= OnViewScrollBottomReached;
                viewInstance.UnreadMessagesSeparatorViewed -= OnViewUnreadMessagesSeparatorViewed;
                viewInstance.FoldingChanged -= OnViewFoldingChanged;
                viewInstance.MemberListVisibilityChanged -= OnViewMemberListVisibilityChanged;
                viewInstance.ChannelRemovalRequested -= OnViewChannelRemovalRequested;
                viewInstance.CurrentChannelChanged -= OnViewCurrentChannelChangedAsync;
                viewInstance.ConversationSelected -= OnSelectConversation;
                viewInstance.DeleteChatHistoryRequested -= OnViewDeleteChatHistoryRequested;
            }

            chatHistory.ChannelAdded -= OnChatHistoryChannelAdded;
            chatHistory.ChannelRemoved -= OnChatHistoryChannelRemoved;
            chatHistory.ReadMessagesChanged -= OnChatHistoryReadMessagesChanged;

            chatUserStateEventBus.FriendConnected -= OnFriendConnected;
            chatUserStateEventBus.UserDisconnected -= OnUserDisconnected;
            chatUserStateEventBus.NonFriendConnected -= OnNonFriendConnected;
            chatUserStateEventBus.CurrentConversationUserAvailable -= OnCurrentConversationUserAvailable;
            chatUserStateEventBus.CurrentConversationUserUnavailable -= OnCurrentConversationUserUnavailable;
            chatUserStateEventBus.UserBlocked -= OnUserBlockedByOwnUser;
            chatUserStateEventBus.UserConnectionStateChanged -= OnUserConnectionStateChanged;

            DCLInput.Instance.Shortcuts.ToggleNametags.performed -= OnToggleNametagsShortcutPerformed;
            DCLInput.Instance.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;

            communitiesEventBus.UserDisconnectedFromCommunity -= OnCommunitiesEventBusUserDisconnectedToCommunity;

            communitiesDataProvider.CommunityCreated -= OnCommunitiesDataProviderCommunityCreated;
            communitiesDataProvider.CommunityDeleted -= OnCommunitiesDataProviderCommunityDeleted;

            userConnectivityInfoProvider.UserConnected -= OnUserConnectivityInfoProviderUserConnected;
            userConnectivityInfoProvider.UserDisconnected -= OnUserConnectivityInfoProviderUserDisconnected;
        }

        private void OnUserConnectivityInfoProviderUserConnected(string userAddress, ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType)
        {
            if(viewInstance!.CurrentChannelId.Equals(channelId))
                viewInstance!.SetOnlineUserAddresses(userConnectivityInfoProvider.GetOnlineUsersInConversation(channelId, channelType));
        }

        private void OnUserConnectivityInfoProviderUserDisconnected(string userAddress, ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType)
        {
            if(viewInstance!.CurrentChannelId.Equals(channelId))
                viewInstance!.SetOnlineUserAddresses(userConnectivityInfoProvider.GetOnlineUsersInConversation(channelId, channelType));
        }

        private async UniTaskVoid ShowErrorNotificationAsync(string errorMessage, CancellationToken ct)
        {
            const int WARNING_MESSAGE_DELAY_MS = 3000;
            warningNotificationView.SetText(errorMessage);
            warningNotificationView.Show(ct);

            await UniTask.Delay(WARNING_MESSAGE_DELAY_MS, cancellationToken: ct);

            warningNotificationView.Hide(ct: ct);
        }

        private void CloseCommunityChat(string communityId)
        {
            // Store the chat as closed
            string allClosedCommunityChats = DCLPlayerPrefs.GetString(closedCommunityChatsKey, string.Empty);
            if (!allClosedCommunityChats.Contains(communityId))
            {
                DCLPlayerPrefs.SetString(closedCommunityChatsKey, $"{allClosedCommunityChats}{communityId},");
                DCLPlayerPrefs.Save();
            }

            // Close the conversation
            ChatChannel.ChannelId communityChannelId = ChatChannel.NewCommunityChannelId(communityId);
            userCommunities.Remove(communityChannelId);
        }

        private void OpenCommunityChat(string communityId)
        {
            // Store the chat as opened
            string allClosedCommunityChats = DCLPlayerPrefs.GetString(closedCommunityChatsKey, string.Empty);
            DCLPlayerPrefs.SetString(closedCommunityChatsKey, allClosedCommunityChats.Replace($"{communityId},", string.Empty));
            DCLPlayerPrefs.Save();

            // Open the conversation
            ChatChannel.ChannelId channelId = ChatChannel.NewCommunityChannelId(communityId);
            ConversationOpened?.Invoke(chatHistory.Channels.ContainsKey(channelId));

            if (!userCommunities.ContainsKey(channelId))
                AddCommunityConversationAsync(communityId, setAsCurrentChannel: true).Forget();
            else
            {
                var channel = chatHistory.AddOrGetChannel(channelId, ChatChannel.ChatChannelType.COMMUNITY);
                viewInstance!.CurrentChannelId = channelId;
                CurrentChannel.UpdateValue(channel);
            }

            SetupViewWithUserStateOnMainThreadAsync(ChatUserStateUpdater.ChatUserState.CONNECTED).Forget();

            chatUsersUpdateCts = chatUsersUpdateCts.SafeRestart();

            viewInstance!.Focus();
        }

        private bool IsCommunityChatClosed(string communityId)
        {
            string allClosedCommunityChats = DCLPlayerPrefs.GetString(closedCommunityChatsKey, string.Empty);
            return allClosedCommunityChats.Contains(communityId);
        }
    }
}
