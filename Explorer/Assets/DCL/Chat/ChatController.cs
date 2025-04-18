using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.CharacterCamera;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.EventBus;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.Profiles;
using DCL.RealmNavigation;
using DCL.Settings.Settings;
using DCL.UI;
using DCL.UI.InputFieldFormatting;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using ECS.Abstract;
using LiveKit.Rooms;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;
using Utility.Arch;

namespace DCL.Chat
{
    public class ChatController : ControllerBase<ChatView, ChatControllerShowParams>, IControllerInSharedSpace<ChatView, ChatControllerShowParams>
    {
        private const string WELCOME_MESSAGE = "Type /help for available commands.";
        private static readonly Color DEFAULT_COLOR = Color.white;

        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly NametagsData nametagsData;
        private readonly IChatHistory chatHistory;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IInputBlock inputBlock;
        private readonly ViewDependencies viewDependencies;
        private readonly IChatCommandsBus chatCommandsBus;
        private readonly IRoom islandRoom;
        private readonly IRoom currentRoom;
        private readonly IProfileCache profileCache;
        private readonly ITextFormatter hyperlinkTextFormatter;
        private readonly ChatSettingsAsset chatSettings;
        private readonly IChatEventBus chatEventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ILoadingStatus loadingStatus;
        private readonly ChatHistoryStorage? chatStorage;
        private readonly ChatUserStateUpdater chatUserStateUpdater;
        private readonly ChatUsersStateCache chatUsersStateCache;
        private readonly IChatUserStateEventBus chatUserStateEventBus;

        private readonly List<ChatMemberListView.MemberData> membersBuffer = new ();
        private readonly List<Profile> participantProfileBuffer = new ();
        private readonly IRoomHub roomHub;

        private CancellationTokenSource chatUsersUpdateCts;
        private SingleInstanceEntity cameraEntity;
        private CancellationTokenSource memberListCts;
        private string previousRoomSid = string.Empty;
        // Used exclusively to calculate the new value of the read messages once the Unread messages separator has been viewed
        private int messageCountWhenSeparatorViewed;
        private bool hasToResetUnreadMessagesWhenNewMessageArrive;
        // We use this to avoid doing null checks after the viewInstance was created
        private bool viewInstanceCreated;

        public ChatController(
            ViewFactoryMethod viewFactory,
            IChatMessagesBus chatMessagesBus,
            IChatHistory chatHistory,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            NametagsData nametagsData,
            World world,
            Entity playerEntity,
            IInputBlock inputBlock,
            ViewDependencies viewDependencies,
            IChatCommandsBus chatCommandsBus,
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
            ObjectProxy<IFriendsService> friendsService) : base(viewFactory)
        {
            this.chatMessagesBus = chatMessagesBus;
            this.chatHistory = chatHistory;
            this.entityParticipantTable = entityParticipantTable;
            this.nametagsData = nametagsData;
            this.world = world;
            this.playerEntity = playerEntity;
            this.inputBlock = inputBlock;
            this.viewDependencies = viewDependencies;
            this.chatCommandsBus = chatCommandsBus;
            this.islandRoom = roomHub.IslandRoom();
            this.roomHub = roomHub;
            this.chatSettings = chatSettings;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
            this.profileCache = profileCache;
            this.chatEventBus = chatEventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.loadingStatus = loadingStatus;
            this.chatStorage = chatStorage;

            chatUsersStateCache = new ChatUsersStateCache();
            chatUserStateEventBus = new ChatUserStateEventBus();
            chatUserStateUpdater = new ChatUserStateUpdater(
                userBlockingCacheProxy,
                roomHub.PrivateConversationsRoom().Participants,
                chatSettings,
                chatPrivacyService,
                chatUserStateEventBus,
                chatUsersStateCache,
                friendsEventBus,
                roomHub.PrivateConversationsRoom(),
                friendsService);
        }

#region Panel Visibility

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

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

                // When opened from outside, it should show the unread messages
                if (value)
                    viewInstance.ShowNewMessages();
            }
        }

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ChatControllerShowParams showParams)
        {
            if (State != ControllerState.ViewHidden)
            {
                if (!viewInstanceCreated)
                    return;

                // If the view is disabled, we re-enable it
                if(!GetViewVisibility())
                    SetViewVisibility(true);

                if(showParams.Unfold)
                    IsUnfolded = true;

                if(showParams.Focus)
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

        public override void Dispose()
        {
            chatStorage?.UnloadAllFiles();
            chatUsersStateCache.ClearAll();
            chatUserStateUpdater.Dispose();
            chatHistory.DeleteAllChannels();
            viewInstance?.RemoveAllConversations();
            memberListCts.SafeCancelAndDispose();
        }

#region View Show and Close

        protected override void OnViewShow()
        {
            cameraEntity = world.CacheCamera();

            viewInstance!.InjectDependencies(viewDependencies);
            viewInstance.Initialize(chatHistory.Channels, chatSettings, GetProfilesFromParticipants, loadingStatus);
            chatStorage?.SetNewLocalUserWalletAddress(web3IdentityCache.Identity!.Address);

            SubscribeToEvents();

            AddNearbyChannelAndSendWelcomeMessage();

            memberListCts = new CancellationTokenSource();
            UniTask.RunOnThreadPool(UpdateMembersDataAsync).Forget();

            InitializeChannelsAndConversationsAsync().Forget();

            IsUnfolded = inputData.Unfold;
            viewInstance.Blur();
        }

        private void AddNearbyChannelAndSendWelcomeMessage()
        {
            chatHistory.AddOrGetChannel(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);
            viewInstance!.CurrentChannelId = ChatChannel.NEARBY_CHANNEL_ID;
            chatHistory.AddMessage(ChatChannel.NEARBY_CHANNEL_ID, ChatMessage.NewFromSystem(WELCOME_MESSAGE));
            chatHistory.Channels[ChatChannel.NEARBY_CHANNEL_ID].MarkAllMessagesAsRead();
        }

        private async UniTaskVoid InitializeChannelsAndConversationsAsync()
        {
            if (chatStorage != null)
                await chatStorage.LoadAllChannelsWithoutMessagesAsync();

            var connectedUsers = await chatUserStateUpdater.InitializeAsync(chatHistory.Channels.Keys);
            viewInstance!.SetupInitialConversationToolbarStatusIconForUsers(connectedUsers);
        }

        protected override void OnViewClose()
        {
            Blur();
            UnsubscribeFromEvents();
            Dispose();
        }

#endregion

#region Other Controller Methods

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            viewInstanceCreated = true;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }
#endregion

#region Conversation Events

        private void OnOpenConversation(string userId)
        {
            ChatChannel channel = chatHistory.AddOrGetChannel(new ChatChannel.ChannelId(userId), ChatChannel.ChatChannelType.USER);
            chatUserStateUpdater.CurrentConversation = userId;
            chatUserStateUpdater.AddConversation(userId);
            viewInstance!.CurrentChannelId = channel.Id;
            chatUsersUpdateCts = chatUsersUpdateCts.SafeRestart();
            UpdateChatUserStateAsync(userId, chatUsersUpdateCts.Token, true).Forget();
        }

        private void OnSelectConversation(ChatChannel.ChannelId channelId)
        {
            chatUserStateUpdater.CurrentConversation = channelId.Id;
            viewInstance!.CurrentChannelId = channelId;

            if (channelId.Equals(ChatChannel.NEARBY_CHANNEL_ID))
            {
                viewInstance!.SetInputWithUserState(ChatUserStateUpdater.ChatUserState.CONNECTED);
            }
            else
            {
                chatUserStateUpdater.AddConversation(channelId.Id);
                chatUsersUpdateCts = chatUsersUpdateCts.SafeRestart();
                UpdateChatUserStateAsync(channelId.Id, chatUsersUpdateCts.Token).Forget();
            }
        }

        private async UniTaskVoid UpdateChatUserStateAsync(string userId, CancellationToken ct, bool updateToolbar = false)
        {
            var userState = await chatUserStateUpdater.GetChatUserStateAsync(userId, ct);
            viewInstance!.SetInputWithUserState(userState);

            if (!updateToolbar) return;

            bool offline = userState == ChatUserStateUpdater.ChatUserState.DISCONNECTED
                           || userState == ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER;

            viewInstance.UpdateConversationToolbarStatusIconForUser(userId, offline? OnlineStatus.OFFLINE : OnlineStatus.ONLINE);
        }
#endregion

#region Chat History Events

        private void OnChatHistoryMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            bool isSentByOwnUser = addedMessage is { IsSystemMessage: false, IsSentByOwnUser: true };

            CreateChatBubble(destinationChannel, addedMessage, isSentByOwnUser);

            if (isSentByOwnUser)
            {
                MarkCurrentChannelAsRead();
                viewInstance!.RefreshMessages();
                viewInstance.ShowLastMessage();
            }
            else
            {
                switch (chatSettings.chatAudioSettings)
                {
                    case ChatAudioSettings.NONE:
                        return;
                    case ChatAudioSettings.MENTIONS_ONLY when addedMessage.IsMention:
                    case ChatAudioSettings.ALL:
                        UIAudioEventsBus.Instance.SendPlayAudioEvent(addedMessage.IsMention ?
                            viewInstance!.ChatReceiveMentionMessageAudio :
                            viewInstance!.ChatReceiveMessageAudio);
                        break;
                }

                // If the chat is showing the channel that receives the message and the scroll view is at the bottom, mark everything as read
                bool shouldMarkChannelAsRead = viewInstance!.IsMessageListVisible && viewInstance.IsScrollAtBottom;

                if (destinationChannel.Id.Equals(viewInstance.CurrentChannelId))
                {
                    if (shouldMarkChannelAsRead)
                        MarkCurrentChannelAsRead();

                    // Note: When the unread messages separator (NEW line) is viewed, it gets ready to jump to a new position.
                    //       Once a new message arrives, the separator moves to the position of that new message and the count of
                    //       unread messages is set to 1.
                    if (hasToResetUnreadMessagesWhenNewMessageArrive)
                    {
                        hasToResetUnreadMessagesWhenNewMessageArrive = false;
                        destinationChannel.ReadMessages = messageCountWhenSeparatorViewed;
                    }

                    viewInstance.RefreshMessages();
                }
                else // Messages arrived to other conversations
                {
                    viewInstance.RefreshUnreadMessages(destinationChannel.Id);
                }
            }
        }

        private void OnChatHistoryReadMessagesChanged(ChatChannel changedChannel)
        {
            if(changedChannel.Id.Equals(viewInstance!.CurrentChannelId))
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
                viewInstance.RefreshMessages();
            }
        }

        private void OnViewChannelRemovalRequested(ChatChannel.ChannelId channelId)
        {
            chatHistory.RemoveChannel(channelId);
        }
#endregion

        private void OnClearChatCommandReceived() // Called by a command
        {
            chatHistory.ClearChannel(viewInstance!.CurrentChannelId);
            messageCountWhenSeparatorViewed = 0;
        }

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

        private void OnViewScrollBottomReached()
        {
            MarkCurrentChannelAsRead();
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
            if (isFocused)
                DisableUnwantedInputs();
            else
                EnableUnwantedInputs();
        }

        private void OnViewPointerExit() =>
            world.TryRemove<CameraBlockerComponent>(cameraEntity);

        private void OnViewPointerEnter() =>
            world.AddOrGet(cameraEntity, new CameraBlockerComponent());

        private void OnOpenChatCommandLineShortcutPerformed(InputAction.CallbackContext obj)
        {
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

        private void OnChatBusMessageAdded(ChatChannel.ChannelId channelId, ChatMessage chatMessage)
        {
            if (!chatMessage.IsSystemMessage)
            {
                string formattedText = hyperlinkTextFormatter.FormatText(chatMessage.Message);
                var newChatMessage = ChatMessage.CopyWithNewMessage(formattedText, chatMessage);
                chatHistory.AddMessage(channelId, newChatMessage);
            }
            else
                chatHistory.AddMessage(channelId, chatMessage);
        }
        private void OnViewMemberListVisibilityChanged(bool isVisible)
        {
            if (isVisible && roomHub.HasAnyRoomConnected())
                RefreshMemberList();
        }

#region ChatBubbles

        private void CreateChatBubble(ChatChannel channel, ChatMessage chatMessage, bool isSentByOwnUser)
        {
            if (!nametagsData.showNameTags || chatSettings.chatBubblesVisibilitySettings == ChatBubbleVisibilitySettings.NONE)
                return;

            if (chatMessage.IsSentByOwnUser == false && entityParticipantTable.TryGet(chatMessage.SenderWalletAddress, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                bool isPrivateMessage = channel.ChannelType == ChatChannel.ChatChannelType.USER;

                // Chat bubbles appears if the channel is nearby or if settings allow them to appear for private conversations
                if (!isPrivateMessage || chatSettings.chatBubblesVisibilitySettings == ChatBubbleVisibilitySettings.ALL)
                    GenerateChatBubbleComponent(entry.Entity, chatMessage, DEFAULT_COLOR, isPrivateMessage, channel.Id);
            }
            else if (isSentByOwnUser)
            {
                if (channel.ChannelType == ChatChannel.ChatChannelType.USER)
                {
                    // Chat bubbles appears if the channel is nearby or if settings allow them to appear for private conversations
                    if (chatSettings.chatBubblesVisibilitySettings == ChatBubbleVisibilitySettings.ALL)
                    {
                        if (!profileCache.TryGet(channel.Id.Id, out var profile))
                        {
                            GenerateChatBubbleComponent(playerEntity, chatMessage, DEFAULT_COLOR, true, channel.Id);
                        }
                        else
                        {
                            Color nameColor = profile.UserNameColor != DEFAULT_COLOR? profile.UserNameColor : ProfileNameColorHelper.GetNameColor(profile.DisplayName);
                            GenerateChatBubbleComponent(playerEntity, chatMessage, nameColor, true, channel.Id, profile.ValidatedName, profile.WalletId);
                        }
                    }
                }
                else
                    GenerateChatBubbleComponent(playerEntity, chatMessage, DEFAULT_COLOR, false, channel.Id);
            }
        }

        private void GenerateChatBubbleComponent(Entity e, ChatMessage chatMessage, Color receiverNameColor, bool isPrivateMessage, ChatChannel.ChannelId messageChannelId, string? receiverDisplayName = null, string? receiverWalletId = null)
        {
                world.AddOrSet(e, new ChatBubbleComponent(
                    chatMessage.Message,
                    chatMessage.SenderValidatedName,
                    chatMessage.SenderWalletAddress,
                    chatMessage.IsMention,
                    isPrivateMessage,
                    messageChannelId.Id,
                    chatMessage.IsSentByOwnUser,
                    receiverDisplayName?? string.Empty,
                    receiverWalletId?? string.Empty,
                    receiverNameColor));
        }
#endregion

#region Member List

        private List<ChatMemberListView.MemberData> GenerateMemberList()
        {
            membersBuffer.Clear();

            GetProfilesFromParticipants(participantProfileBuffer);

            for (int i = 0; i < participantProfileBuffer.Count; ++i)
            {
                ChatMemberListView.MemberData newMember = GetMemberDataFromParticipantIdentity(participantProfileBuffer[i]);

                if (!string.IsNullOrEmpty(newMember.Name))
                    membersBuffer.Add(newMember);
            }

            return membersBuffer;
        }

        private ChatMemberListView.MemberData GetMemberDataFromParticipantIdentity(Profile profile)
        {
            ChatMemberListView.MemberData newMemberData = new ChatMemberListView.MemberData
                {
                    Id = profile.UserId,
                };

            if (profile != null)
            {
                newMemberData.Name = profile.ValidatedName;
                newMemberData.FaceSnapshotUrl = profile.Avatar.FaceSnapshotUrl;
                newMemberData.ConnectionStatus = ChatMemberConnectionStatus.Online; // TODO: Get this info from somewhere, when the other shapes are developed
                newMemberData.WalletId = profile.WalletId;
                newMemberData.ProfileColor = profile.UserNameColor;
            }

            return newMemberData;
        }

        private void RefreshMemberList()
        {
            List<ChatMemberListView.MemberData> members = GenerateMemberList();
            viewInstance!.SetMemberData(members);
        }

        private void GetProfilesFromParticipants(List<Profile> outProfiles)
        {
            outProfiles.Clear();

            foreach (string? identity in roomHub.AllRoomsRemoteParticipantIdentities())
            {
                // TODO: Use new endpoint to get a bunch of profile info
                if (profileCache.TryGet(identity, out var profile))
                    outProfiles.Add(profile);
            }
        }

        private async UniTask UpdateMembersDataAsync()
        {
            //TODO FRAN: Check this code and improve it
            const int WAIT_TIME_IN_BETWEEN_UPDATES = 500;

            while (!memberListCts.IsCancellationRequested)
            {
                // If the player jumps to another island room (like a world) while the member list is visible, it must refresh
                if (previousRoomSid != islandRoom.Info.Sid && viewInstance!.IsMemberListVisible)
                {
                    previousRoomSid = islandRoom.Info.Sid;
                    RefreshMemberList();
                }

                // Updates the amount of members
                int participantsCount = roomHub.ParticipantsCount();
                if(roomHub.HasAnyRoomConnected() && participantsCount != viewInstance!.MemberCount)
                    viewInstance.MemberCount = participantsCount;

                await UniTask.Delay(WAIT_TIME_IN_BETWEEN_UPDATES, cancellationToken: memberListCts.Token);
            }
        }

#endregion

#region Chat History Channel Events
        private void OnChatHistoryChannelRemoved(ChatChannel.ChannelId removedChannel)
        {
            chatUserStateUpdater.RemoveConversation(removedChannel.Id);
            viewInstance!.RemoveConversation(removedChannel);
        }

        private void OnChatHistoryChannelAdded(ChatChannel addedChannel)
        {
            chatUserStateUpdater.AddConversation(addedChannel.Id.Id);
            viewInstance!.AddConversation(addedChannel);
        }
#endregion

#region User State Update Events

        private void OnUserDisconnected(string userId)
        {
            viewInstance!.UpdateConversationToolbarStatusIconForUser(userId, OnlineStatus.OFFLINE);
            if (viewInstance.CurrentChannelId.Id == userId)
            {
                var state = chatUserStateUpdater.GetDisconnectedUserState(userId);
                viewInstance.SetInputWithUserState(state);
            }
        }

        private void OnNonFriendConnected(string userId)
        {
            viewInstance!.UpdateConversationToolbarStatusIconForUser(userId, OnlineStatus.ONLINE);
            if (viewInstance.CurrentChannelId.Id == userId)
                GetAndSetupNonFriendUserStateAsync(userId).Forget();
        }

        private async UniTaskVoid GetAndSetupNonFriendUserStateAsync(string userId)
        {
            var state = await chatUserStateUpdater.GetConnectedNonFriendUserStateAsync(userId);
            viewInstance!.SetInputWithUserState(state);
        }

        private void OnFriendConnected(string userId)
        {
            viewInstance!.UpdateConversationToolbarStatusIconForUser(userId, OnlineStatus.ONLINE);
            if (viewInstance!.CurrentChannelId.Id == userId)
            {
                var state = ChatUserStateUpdater.ChatUserState.CONNECTED;
                viewInstance.SetInputWithUserState(state);
            }
        }

        private void OnUserBlockedByOwnUser(string userId)
        {
            viewInstance!.UpdateConversationToolbarStatusIconForUser(userId, OnlineStatus.OFFLINE);
            if (viewInstance!.CurrentChannelId.Id == userId)
            {
                var state = ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER;
                viewInstance.SetInputWithUserState(state);
            }
        }

        private void OnCurrentConversationUserUnavailable()
        {
            var state = ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED;
            viewInstance!.SetInputWithUserState(state);
        }

        private void OnCurrentConversationUserAvailable()
        {
            var state = ChatUserStateUpdater.ChatUserState.CONNECTED;
            viewInstance!.SetInputWithUserState(state);
        }
        #endregion

        private void SubscribeToEvents()
        {
            //We start processing messages once the view is ready
            chatMessagesBus.MessageAdded += OnChatBusMessageAdded;
            chatCommandsBus.ClearChat += OnClearChatCommandReceived;

            chatEventBus.InsertTextInChat += OnTextInserted;
            chatEventBus.OpenConversation += OnOpenConversation;

            viewInstance.PointerEnter += OnViewPointerEnter;
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

            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed += OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed += OnOpenChatCommandLineShortcutPerformed;
        }

        private void UnsubscribeFromEvents()
        {
            chatMessagesBus.MessageAdded -= OnChatBusMessageAdded;
            chatHistory.MessageAdded -= OnChatHistoryMessageAdded;
            chatHistory.ReadMessagesChanged -= OnChatHistoryReadMessagesChanged;
            chatCommandsBus.ClearChat -= OnClearChatCommandReceived;
            chatEventBus.InsertTextInChat -= OnTextInserted;

            if (viewInstance != null)
            {
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
                viewInstance.RemoveAllConversations();
                viewInstance.Dispose();
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

            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed -= OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;

            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed -= OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;
        }
    }
}
