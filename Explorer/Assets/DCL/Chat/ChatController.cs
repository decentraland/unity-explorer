using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.CharacterCamera;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.EventBus;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.RealmNavigation;
using DCL.Settings.Settings;
using DCL.UI;
using DCL.UI.InputFieldFormatting;
using DCL.Web3.Identities;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using LiveKit.Rooms;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.InputSystem;
using Utility;
using Utility.Arch;
using Utility.Types;

namespace DCL.Chat
{
    public class ChatController : ControllerBase<ChatView, ChatControllerShowParams>,
        IControllerInSharedSpace<ChatView, ChatControllerShowParams>
    {
        public delegate void ConversationOpenedDelegate(bool wasAlreadyOpen);
        public delegate void ConversationClosedDelegate();

        private const string WELCOME_MESSAGE = "Type /help for available commands.";
        private const string NEW_CHAT_MESSAGE = "The chat starts here! Time to say hi! \\U0001F44B";

        private readonly IChatMessagesBus chatMessagesBus;
        private readonly NametagsData nametagsData;
        private readonly IChatHistory chatHistory;
        private readonly World world;
        private readonly IInputBlock inputBlock;
        private readonly ViewDependencies viewDependencies;
        private readonly IRoom islandRoom;
        private readonly IProfileCache profileCache;
        private readonly ITextFormatter hyperlinkTextFormatter;
        private readonly ChatSettingsAsset chatSettings;
        private readonly IChatEventBus chatEventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ILoadingStatus loadingStatus;
        private readonly ChatHistoryStorage? chatStorage;
        private readonly ChatUserStateUpdater chatUserStateUpdater;
        private readonly IChatUserStateEventBus chatUserStateEventBus;
        private readonly ChatControllerChatBubblesHelper chatBubblesHelper;
        private readonly ChatControllerMemberListHelper memberListHelper;
        private readonly IRoomHub roomHub;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

        private readonly List<ChatMemberListView.MemberData> membersBuffer = new ();
        private readonly List<Profile> participantProfileBuffer = new ();

        private SingleInstanceEntity cameraEntity;

        // Used exclusively to calculate the new value of the read messages once the Unread messages separator has been viewed
        private int messageCountWhenSeparatorViewed;
        private bool hasToResetUnreadMessagesWhenNewMessageArrive;
        private bool viewInstanceCreated;
        private CancellationTokenSource chatUsersUpdateCts = new();

        public string IslandRoomSid => islandRoom.Info.Sid;
        public string PreviousRoomSid { get; set; } = string.Empty;

        public event ConversationOpenedDelegate? ConversationOpened;
        public event ConversationClosedDelegate? ConversationClosed;
        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        private readonly ObjectProxy<IFriendsService> friendsServiceProxy;

        public bool TryGetView(out ChatView view)
        {
            view = viewInstance!;
            return viewInstanceCreated && view != null;
        }

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
            ProfileRepositoryWrapper profileDataProvider) : base(viewFactory)
        {
            this.chatMessagesBus = chatMessagesBus;
            this.chatHistory = chatHistory;
            this.nametagsData = nametagsData;
            this.world = world;
            this.inputBlock = inputBlock;
            this.viewDependencies = viewDependencies;
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
                profileCache,
                membersBuffer,
                participantProfileBuffer,
                this);
        }

#region Panel Visibility

        public bool IsVisibleInSharedSpace => State != ControllerState.ViewHidden && GetViewVisibility() && viewInstance!.IsUnfolded;

        /// <summary>
        /// The chat is considered Folded when its hidden either through the sidebar button or through the close button on the chat title bar.
        /// In this state it won't display any chat message, just the empty input box. And Unread messages will accumulate on the sidebar chat icon.
        /// </summary>
        public bool IsUnfolded
        {
            get => viewInstanceCreated && viewInstance.IsUnfolded;

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

#region View Show and Close

        protected override void OnViewShow()
        {
            cameraEntity = world.CacheCamera();

            viewInstance!.InjectDependencies(viewDependencies);
            viewInstance.SetProfileDataPovider(profileRepositoryWrapper);
            viewInstance.Initialize(chatHistory.Channels, chatSettings, GetProfilesFromParticipants, loadingStatus);
            chatStorage?.SetNewLocalUserWalletAddress(web3IdentityCache.Identity!.Address);

            SubscribeToEvents();

            AddNearbyChannelAndSendWelcomeMessage();

            IsUnfolded = inputData.Unfold;
            viewInstance.Blur();

            //We need the friends service enabled to be able to interact with them via chat.
            //If there is no friends service (like in LSD) these two methods should not be invoked
            if (friendsServiceProxy.Configured)
            {
                memberListHelper.StartUpdating();
                InitializeChannelsAndConversationsAsync().Forget();
            }
        }

        protected override void OnBlur()
        {
            viewInstance?.UnsubscribeToSubmitEvent();
        }

        protected override void OnFocus()
        {
            viewInstance?.SubscribeToSubmitEvent();
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
                chatStorage.LoadAllChannelsWithoutMessages();

            var connectedUsers = await chatUserStateUpdater.InitializeAsync(chatHistory.Channels.Keys);

            await UniTask.SwitchToMainThread();
            viewInstance!.SetupInitialConversationToolbarStatusIconForUsers(connectedUsers);
        }

        protected override void OnViewClose()
        {
            Blur();
            UnsubscribeFromEvents();
            Dispose();
        }

#endregion

#region Other Controller-inherited Methods

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

        public override void Dispose()
        {
            chatStorage?.UnloadAllFiles();
            chatUserStateUpdater.Dispose();
            chatHistory.DeleteAllChannels();
            viewInstance?.RemoveAllConversations();
            memberListHelper.StopUpdating();
            chatUsersUpdateCts.SafeCancelAndDispose();
        }

#endregion

#region Conversation Events

        private void OnOpenConversation(string userId)
        {
            ConversationOpened?.Invoke(chatHistory.Channels.ContainsKey(new ChatChannel.ChannelId(userId)));

            var channelId = new ChatChannel.ChannelId(userId);
            ChatChannel channel = chatHistory.AddOrGetChannel(channelId, ChatChannel.ChatChannelType.USER);
            chatUserStateUpdater.CurrentConversation = userId;
            chatUserStateUpdater.AddConversation(userId);

            if (TryGetView(out var view))
                view.CurrentChannelId = channel.Id;

            chatUsersUpdateCts = chatUsersUpdateCts.SafeRestart();
            UpdateChatUserStateAsync(userId, true, chatUsersUpdateCts.Token).Forget();

            viewInstance!.Focus();
        }

        private void OnSelectConversation(ChatChannel.ChannelId channelId)
        {
            chatUserStateUpdater.CurrentConversation = channelId.Id;
            if (TryGetView(out var view))
            {
                view.CurrentChannelId = channelId;

                if (channelId.Equals(ChatChannel.NEARBY_CHANNEL_ID))
                {
                    view.SetInputWithUserState(ChatUserStateUpdater.ChatUserState.CONNECTED);
                    return;
                }
            }

            chatUserStateUpdater.AddConversation(channelId.Id);
            chatUsersUpdateCts = chatUsersUpdateCts.SafeRestart();
            UpdateChatUserStateAsync(channelId.Id, true, chatUsersUpdateCts.Token).Forget();
        }

        private async UniTaskVoid UpdateChatUserStateAsync(string userId, bool updateToolbar, CancellationToken ct)
        {
            Result<ChatUserStateUpdater.ChatUserState> result = await chatUserStateUpdater.GetChatUserStateAsync(userId, ct).SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);
            if (result.Success == false) return;

            ChatUserStateUpdater.ChatUserState userState = result.Value;

            if (TryGetView(out var view))
            {
                view.SetInputWithUserState(userState);

                if (!updateToolbar) return;

                bool offline = userState == ChatUserStateUpdater.ChatUserState.DISCONNECTED
                             || userState == ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER;

                view.UpdateConversationToolbarStatusIconForUser(userId, offline ? OnlineStatus.OFFLINE : OnlineStatus.ONLINE);
            }
        }

#endregion

#region Chat History Events

        private void OnChatHistoryMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            bool isSentByOwnUser = addedMessage is { IsSystemMessage: false, IsSentByOwnUser: true };

            chatBubblesHelper.CreateChatBubble(destinationChannel, addedMessage, isSentByOwnUser);

            if (isSentByOwnUser)
            {
                MarkCurrentChannelAsRead();
                if (TryGetView(out var view))
                {
                    view.RefreshMessages();
                    view.ShowLastMessage();
                }
                return;
            }

            HandleMessageAudioFeedback(addedMessage);

            if (TryGetView(out var currentView))
            {
                bool shouldMarkChannelAsRead = currentView is { IsMessageListVisible: true, IsScrollAtBottom: true };
                bool isCurrentChannel = destinationChannel.Id.Equals(currentView.CurrentChannelId);

                if (isCurrentChannel)
                {
                    if (shouldMarkChannelAsRead)
                        MarkCurrentChannelAsRead();

                    HandleUnreadMessagesSeparator(destinationChannel);
                    currentView.RefreshMessages();
                }
                else
                {
                    currentView.RefreshUnreadMessages(destinationChannel.Id);
                }
            }
        }

        private void HandleMessageAudioFeedback(ChatMessage message)
        {
            if (!TryGetView(out var view))
                return;

            switch (chatSettings.chatAudioSettings)
            {
                case ChatAudioSettings.NONE:
                    return;
                case ChatAudioSettings.MENTIONS_ONLY when message.IsMention:
                case ChatAudioSettings.ALL:
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(message.IsMention ?
                        view.ChatReceiveMentionMessageAudio :
                        view.ChatReceiveMessageAudio);
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

                if (chatHistory.Channels[viewInstance.CurrentChannelId].Messages.Count == 0)
                    chatHistory.AddMessage(viewInstance.CurrentChannelId, ChatMessage.NewFromSystem(NEW_CHAT_MESSAGE));

                viewInstance.RefreshMessages();
            }
        }

        private void OnViewChannelRemovalRequested(ChatChannel.ChannelId channelId)
        {
            ConversationClosed?.Invoke();

            chatHistory.RemoveChannel(channelId);
        }

#endregion

#region View state changes event handling

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
            if (isFocused)
                DisableUnwantedInputs();
            else
                EnableUnwantedInputs();
        }

        private void OnViewPointerExit() =>
            world.TryRemove<CameraBlockerComponent>(cameraEntity);

        private void OnViewPointerEnter() =>
            world.AddOrGet(cameraEntity, new CameraBlockerComponent());

        private void OnViewScrollBottomReached()
        {
            MarkCurrentChannelAsRead();
        }

        private void OnViewMemberListVisibilityChanged(bool isVisible)
        {
            if (isVisible && roomHub.HasAnyRoomConnected())
                RefreshMemberList();
        }

        private void RefreshMemberList()
        {
            memberListHelper.RefreshMemberList();
        }

#endregion

#region External components event handling

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
            var state = chatUserStateUpdater.GetDisconnectedUserState(userId);
            viewInstance!.SetInputWithUserState(state);
        }

        private void OnNonFriendConnected(string userId)
        {
            GetAndSetupNonFriendUserStateAsync(userId).Forget();
        }

        private async UniTaskVoid GetAndSetupNonFriendUserStateAsync(string userId)
        {
            //We might need a new state of type "LOADING" or similar to display until we resolve the real state
            viewInstance!.SetInputWithUserState(ChatUserStateUpdater.ChatUserState.DISCONNECTED);
            var state = await chatUserStateUpdater.GetConnectedNonFriendUserStateAsync(userId);
            viewInstance!.SetInputWithUserState(state);
        }

        private void OnFriendConnected(string userId)
        {
            var state = ChatUserStateUpdater.ChatUserState.CONNECTED;
            viewInstance!.SetInputWithUserState(state);

        }

        private void OnUserBlockedByOwnUser(string userId)
        {
            var state = ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER;
            viewInstance!.SetInputWithUserState(state);
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

        private void OnUserConnectionStateChanged(string userId, bool isConnected)
        {
            viewInstance!.UpdateConversationToolbarStatusIconForUser(userId, isConnected? OnlineStatus.ONLINE : OnlineStatus.OFFLINE);
        }

        #endregion

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

        private void GetProfilesFromParticipants(List<Profile> outProfiles)
        {
            outProfiles.Clear();

            foreach (string? identity in roomHub.AllLocalRoomsRemoteParticipantIdentities())
            {
                // TODO: Use new endpoint to get a bunch of profile info
                if (profileCache.TryGet(identity, out var profile))
                    outProfiles.Add(profile);
            }
        }

        private void SubscribeToEvents()
        {
            //We start processing messages once the view is ready
            chatMessagesBus.MessageAdded += OnChatBusMessageAdded;

            chatEventBus.InsertTextInChat += OnTextInserted;
            chatEventBus.OpenConversation += OnOpenConversation;

            if (TryGetView(out var view))
            {
                view.PointerEnter += OnViewPointerEnter;
                view.PointerExit += OnViewPointerExit;

                view.FocusChanged += OnViewFocusChanged;
                view.EmojiSelectionVisibilityChanged += OnViewEmojiSelectionVisibilityChanged;
                view.InputSubmitted += OnViewInputSubmitted;
                view.MemberListVisibilityChanged += OnViewMemberListVisibilityChanged;
                view.ScrollBottomReached += OnViewScrollBottomReached;
                view.UnreadMessagesSeparatorViewed += OnViewUnreadMessagesSeparatorViewed;
                view.FoldingChanged += OnViewFoldingChanged;
                view.ChannelRemovalRequested += OnViewChannelRemovalRequested;
                view.CurrentChannelChanged += OnViewCurrentChannelChangedAsync;
                view.ConversationSelected += OnSelectConversation;
                view.DeleteChatHistoryRequested += OnViewDeleteChatHistoryRequested;
                
            }

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

            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed += OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed += OnOpenChatCommandLineShortcutPerformed;
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
                viewInstance.DeleteChatHistoryRequested -= OnViewDeleteChatHistoryRequested;
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
            chatUserStateEventBus.UserConnectionStateChanged -= OnUserConnectionStateChanged;

            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed -= OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;
        }
    }
}
