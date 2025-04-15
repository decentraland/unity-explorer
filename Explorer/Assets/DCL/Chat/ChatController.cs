using Arch.Core;
using Cysharp.Threading.Tasks;
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
using DCL.Web3.Identities;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using ECS.Abstract;
using LiveKit.Rooms;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.InputSystem;
using Utility.Arch;

namespace DCL.Chat
{
    public class ChatController : ControllerBase<ChatView, ChatControllerShowParams>,
        IControllerInSharedSpace<ChatView, ChatControllerShowParams>, IChatController
    {
        private const string WELCOME_MESSAGE = "Type /help for available commands.";

        private readonly IChatMessagesBus chatMessagesBus;
        private readonly NametagsData nametagsData;
        private readonly IChatHistory chatHistory;
        private readonly World world;
        private readonly IInputBlock inputBlock;
        private readonly ViewDependencies viewDependencies;
        private readonly IChatCommandsBus chatCommandsBus;
        private readonly IRoom islandRoom;
        private readonly IProfileCache profileCache;
        private readonly ChatSettingsAsset chatSettings;
        private readonly IChatEventBus chatEventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ILoadingStatus loadingStatus;
        private readonly ChatHistoryStorage? chatStorage;
        private readonly ChatUserStateUpdater chatUserStateUpdater;
        private readonly ChatUsersStateCache chatUsersStateCache;
        private readonly IChatUserStateEventBus chatUserStateEventBus;
        private readonly ChatControllerMemberListHelper memberListHelper;
        private readonly ChatControllerConversationEventsHelper conversationEventsHelper;
        private readonly ChatControllerUserStateHelper userStateHelper;
        private readonly ChatControllerMessageHandlingHelper messageHandlingHelper;
        private readonly ChatControllerInputHelper inputHelper;

        private readonly List<ChatMemberListView.MemberData> membersBuffer = new ();
        private readonly List<Profile> participantProfileBuffer = new ();
        private readonly IRoomHub roomHub;

        private SingleInstanceEntity cameraEntity;

        // We use this to avoid doing null checks after the viewInstance was created
        private bool viewInstanceCreated;

        string IChatController.IslandRoomSid => islandRoom.Info.Sid;
        string IChatController.PreviousRoomSid { get; set; } = string.Empty;

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
            this.nametagsData = nametagsData;
            this.world = world;
            this.inputBlock = inputBlock;
            this.viewDependencies = viewDependencies;
            this.chatCommandsBus = chatCommandsBus;
            this.islandRoom = roomHub.IslandRoom();
            this.roomHub = roomHub;
            this.chatSettings = chatSettings;
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

            var chatBubblesHelper = new ChatControllerChatBubblesHelper(
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

            conversationEventsHelper = new ChatControllerConversationEventsHelper(
                chatHistory,
                chatUserStateUpdater,
                this,
                chatStorage);

            userStateHelper = new ChatControllerUserStateHelper(
                chatUserStateUpdater,
                this);

            messageHandlingHelper = new ChatControllerMessageHandlingHelper(
                chatHistory,
                this,
                chatBubblesHelper,
                chatSettings,
                hyperlinkTextFormatter);

            inputHelper = new ChatControllerInputHelper(
                world,
                inputBlock,
                this,
                cameraEntity,
                nametagsData,
                chatMessagesBus);
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
            }
        }

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ChatControllerShowParams showParams)
        {
            if (State != ControllerState.ViewHidden)
            {
                if (!viewInstanceCreated) return;

                //If the view is disabled, we re-enable it
                if(!GetViewVisibility())
                    SetViewVisibility(true);

                //TODO FRAN: here we must restore the state of the chat when returning to it from anywhere, unless overwritten for some reason.
                //This should be the only way to open the chat, params should adjust to these possibilities.

                //if(showParams.ShowUnfolded)
                IsUnfolded = true;//showParams.ShowUnfolded;

                if(showParams.HasToFocusInputBox)
                    viewInstance!.FocusInputBox();

                if (showParams.ShowLastState)
                {
                    //IsUnfolded = !viewInstance!.LastChatState.HasFlag(ChatView.ChatState.FOLDED);
                }

                ViewShowingComplete?.Invoke(this);
            }

            await UniTask.CompletedTask;
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            // TODO FRAN: We only need to minimize the chat when the sidebar chat button is pressed or the enter key is pressed,
            // in the other cases, we want to preserve their last state, how to do it??
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
            memberListHelper.StopUpdating();
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

            memberListHelper.StartUpdating();

            InitializeChannelsAndConversationsAsync().Forget();

            OnFocus();
            IsUnfolded = inputData.ShowUnfolded;
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
            UnsubscribeFromEvents();
            memberListHelper.StopUpdating();
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

        protected override void OnBlur()
        {
            //TODO FRAN: Check what is this doing if its doing anything
            viewInstance!.IsChatSelected = false;
            //viewInstance!.DisableInputBoxSubmissions();
        }

        protected override void OnFocus()
        {
            //TODO FRAN: Check what is this doing
            if (viewInstance!.IsFocused) return;

            IsUnfolded = true;
            //viewInstance.EnableInputBoxSubmissions();
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
            conversationEventsHelper.OnOpenConversation(userId);
        }

        private void OnSelectConversation(ChatChannel.ChannelId channelId)
        {
            conversationEventsHelper.OnSelectConversation(channelId);
        }
#endregion

#region Chat History Events
        private void OnChatHistoryMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            messageHandlingHelper.OnChatHistoryMessageAdded(destinationChannel, addedMessage);
        }

        private void OnChatHistoryReadMessagesChanged(ChatChannel changedChannel)
        {
            messageHandlingHelper.OnChatHistoryReadMessagesChanged(changedChannel);
        }

        private void OnViewUnreadMessagesSeparatorViewed()
        {
            messageHandlingHelper.OnUnreadMessagesSeparatorViewed();
        }

        private void OnViewScrollBottomReached()
        {
            messageHandlingHelper.OnScrollBottomReached();
        }

        private void OnClearChatCommandReceived()
        {
            messageHandlingHelper.ClearChannel();
        }
#endregion

#region Channel Events
        private async void OnViewCurrentChannelChangedAsync()
        {
            await conversationEventsHelper.OnCurrentChannelChangedAsync();
        }

        private void OnViewChannelRemovalRequested(ChatChannel.ChannelId channelId)
        {
            conversationEventsHelper.OnChannelRemovalRequested(channelId);
        }
#endregion

        private void OnViewFoldingChanged(bool isUnfolded)
        {
            //TODO FRAN: Check what is this doing
            if (!isUnfolded)
                messageHandlingHelper.MarkCurrentChannelAsRead();
        }

        private void SubscribeToEvents()
        {
            //We start processing messages once the view is ready
            chatMessagesBus.MessageAdded += OnChatBusMessageAdded;
            chatCommandsBus.ClearChat += OnClearChatCommandReceived;

            chatEventBus.InsertTextInChat += OnTextInserted;
            chatEventBus.OpenConversation += OnOpenConversation;

            if (TryGetView(out var view))
            {
                view.PointerEnter += inputHelper.OnViewPointerEnter;
                view.PointerExit += inputHelper.OnViewPointerExit;

                view.ChatSelectStateChanged += inputHelper.OnViewChatSelectStateChanged;
                view.EmojiSelectionVisibilityChanged += inputHelper.OnViewEmojiSelectionVisibilityChanged;
                view.InputSubmitted += inputHelper.OnViewInputSubmitted;
                view.MemberListVisibilityChanged += OnViewMemberListVisibilityChanged;
                view.ScrollBottomReached += OnViewScrollBottomReached;
                view.UnreadMessagesSeparatorViewed += OnViewUnreadMessagesSeparatorViewed;
                view.FoldingChanged += OnViewFoldingChanged;
                view.ChannelRemovalRequested += OnViewChannelRemovalRequested;
                view.CurrentChannelChanged += OnViewCurrentChannelChangedAsync;
                view.ConversationSelected += OnSelectConversation;
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

            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed += inputHelper.OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed += inputHelper.OnOpenChatCommandLineShortcutPerformed;
        }

        private void UnsubscribeFromEvents()
        {
            chatMessagesBus.MessageAdded -= OnChatBusMessageAdded;
            chatHistory.MessageAdded -= OnChatHistoryMessageAdded;
            chatHistory.ReadMessagesChanged -= OnChatHistoryReadMessagesChanged;
            chatCommandsBus.ClearChat -= OnClearChatCommandReceived;
            chatEventBus.InsertTextInChat -= OnTextInserted;

            if (TryGetView(out var view))
            {
                view.PointerEnter -= inputHelper.OnViewPointerEnter;
                view.PointerExit -= inputHelper.OnViewPointerExit;
                view.ChatSelectStateChanged -= inputHelper.OnViewChatSelectStateChanged;
                view.EmojiSelectionVisibilityChanged -= inputHelper.OnViewEmojiSelectionVisibilityChanged;
                view.InputSubmitted -= inputHelper.OnViewInputSubmitted;
                view.ScrollBottomReached -= OnViewScrollBottomReached;
                view.UnreadMessagesSeparatorViewed -= OnViewUnreadMessagesSeparatorViewed;
                view.FoldingChanged -= OnViewFoldingChanged;
                view.MemberListVisibilityChanged -= OnViewMemberListVisibilityChanged;
                view.ChannelRemovalRequested -= OnViewChannelRemovalRequested;
                view.CurrentChannelChanged -= OnViewCurrentChannelChangedAsync;
                view.ConversationSelected -= OnSelectConversation;
                view.RemoveAllConversations();
                view.Dispose();
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

            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed -= inputHelper.OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed -= inputHelper.OnOpenChatCommandLineShortcutPerformed;
        }

        public void SetInputWithUserState(ChatUserStateUpdater.ChatUserState state)
        {
            if (TryGetView(out var view))
            {
                view.SetInputWithUserState(state);
            }
        }

        public void UpdateConversationToolbarStatusIcon(string userId, OnlineStatus status)
        {
            if (TryGetView(out var view))
            {
                view.UpdateConversationToolbarStatusIconForUser(userId, status);
            }
        }

        private void OnViewMemberListVisibilityChanged(bool isVisible)
        {
            memberListHelper.OnViewMemberListVisibilityChanged(isVisible);
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
            userStateHelper.OnUserDisconnected(userId);
        }

        private void OnNonFriendConnected(string userId)
        {
            userStateHelper.OnNonFriendConnected(userId);
        }

        private void OnFriendConnected(string userId)
        {
            userStateHelper.OnFriendConnected(userId);
        }

        private void OnUserBlockedByOwnUser(string _)
        {
            userStateHelper.OnUserBlockedByOwnUser();
        }

        private void OnCurrentConversationUserUnavailable()
        {
            userStateHelper.OnCurrentConversationUserUnavailable();
        }

        private void OnCurrentConversationUserAvailable()
        {
            userStateHelper.OnCurrentConversationUserAvailable();
        }

        private void OnUserConnectionStateChanged(string userId, bool isConnected)
        {
            userStateHelper.OnUserConnectionStateChanged(userId, isConnected);
        }

#endregion

        private void OnTextInserted(string text)
        {
            inputHelper.OnTextInserted(text);
        }

        private void OnChatBusMessageAdded(ChatChannel.ChannelId channelId, ChatMessage chatMessage)
        {
            messageHandlingHelper.OnChatBusMessageAdded(channelId, chatMessage);
        }
    }
}
