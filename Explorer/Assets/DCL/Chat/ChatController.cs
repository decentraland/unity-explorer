using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.EventBus;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.Profiles;
using DCL.RealmNavigation;
using DCL.Settings.Settings;
using DCL.UI.InputFieldFormatting;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.UI.SharedSpaceManager;
using ECS.Abstract;
using LiveKit.Proto;
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
    public class ChatController : ControllerBase<ChatView, ChatController.ShowParams>, IControllerInSharedSpace<ChatView, ChatController.ShowParams>
    {
        public struct ShowParams
        {
            /// <summary>
            /// Indicates whether the chat panel should be folded or unfolded when its view is shown.
            /// </summary>
            public readonly bool ShowUnfolded;

            /// <summary>
            /// Indicates whether the input box of the chat panel should gain the focus after showing.
            /// </summary>
            public readonly bool HasToFocusInputBox;

            /// <summary>
            /// Constructor with all fields.
            /// </summary>
            /// <param name="showUnfolded">Indicates whether the chat panel should be folded or unfolded when its view is shown.</param>
            /// <param name="hasToFocusInputBox">Indicates whether the input box of the chat panel should gain the focus after showing</param>
            public ShowParams(bool showUnfolded, bool hasToFocusInputBox = false)
            {
                ShowUnfolded = showUnfolded;
                HasToFocusInputBox = hasToFocusInputBox;
            }
        }

        public delegate void ChatBubbleVisibilityChangedDelegate(bool isVisible);
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
        private readonly ChatAudioSettingsAsset chatAudioSettings;
        private readonly IChatEventBus chatEventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ILoadingStatus loadingStatus;
        private readonly ChatHistoryStorage? chatStorage;

        private SingleInstanceEntity cameraEntity;
        private CancellationTokenSource memberListCts;
        private string previousRoomSid = string.Empty;
        private readonly List<ChatMemberListView.MemberData> membersBuffer = new ();
        private readonly List<Profile> participantProfileBuffer = new ();

        // Used exclusively to calculate the new value of the read messages once the Unread messages separator has been viewed
        private int messageCountWhenSeparatorViewed;
        private bool hasToResetUnreadMessagesWhenNewMessageArrive;
        private Web3Address currentUserAddress;
        private bool canUpdateParticipants => islandRoom.Info.ConnectionState == ConnectionState.ConnConnected;
        private readonly IRoomHub roomHub;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public bool IsUnfolded
        {
            get => viewInstance.IsUnfolded;

            set
            {
                viewInstance.IsUnfolded = value;

                // When opened from outside, it should show the unread messages
                if (value)
                    viewInstance.ShowNewMessages();

                if (value)
                    viewDependencies.DclInput.UI.Submit.performed += OnSubmitShortcutPerformed;
                else
                    viewDependencies.DclInput.UI.Submit.performed -= OnSubmitShortcutPerformed;
            }

        }

        public bool IsVisibleInSharedSpace => State != ControllerState.ViewHidden && GetViewVisibility() && viewInstance!.IsUnfolded;

        public event ChatBubbleVisibilityChangedDelegate? ChatBubbleVisibilityChanged;
        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

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
            ChatAudioSettingsAsset chatAudioSettings,
            ITextFormatter hyperlinkTextFormatter,
            IProfileCache profileCache,
            IChatEventBus chatEventBus,
            IWeb3IdentityCache web3IdentityCache,
            ILoadingStatus loadingStatus,
            ChatHistoryStorage chatStorage) : base(viewFactory)
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
            this.chatAudioSettings = chatAudioSettings;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
            this.profileCache = profileCache;
            this.chatEventBus = chatEventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.loadingStatus = loadingStatus;
            this.chatStorage = chatStorage;
        }

        public void Clear() // Called by a command
        {
            chatHistory.ClearChannel(viewInstance!.CurrentChannelId);
            messageCountWhenSeparatorViewed = 0;
        }

        public override void Dispose()
        {
            chatStorage.UnloadAllFiles();

            memberListCts.SafeCancelAndDispose();
        }

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ShowParams showParams)
        {
            if(State != ControllerState.ViewHidden)
            {
                if(!GetViewVisibility())
                    SetViewVisibility(true);

                IsUnfolded = showParams.ShowUnfolded;

                if(showParams.HasToFocusInputBox)
                    FocusInputBox();

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
        /// Makes the input box gain the focus so the user can start typing.
        /// </summary>
        public void FocusInputBox()
        {
            viewInstance.FocusInputBox();
        }

        /// <summary>
        /// Makes the chat panel (including the input box) invisible or visible (it does not hide the view).
        /// </summary>
        /// <param name="visibility">Whether to make the panel visible.</param>
        public void SetViewVisibility(bool visibility)
        {
            viewInstance.gameObject.SetActive(visibility);
        }

        /// <summary>
        /// Indicates whether the panel is invisible or not (the view is never hidden as it is a Persistent panel).
        /// </summary>
        /// <returns>True if the panel is visible; False otherwise.</returns>
        public bool GetViewVisibility()
        {
            return viewInstance.gameObject.activeInHierarchy;
        }

        private void OnChatHistoryAllChannelsRemoved()
        {
            viewInstance!.RemoveAllConversations();
        }

        private void ShowWelcomeMessage()
        {
            chatHistory.AddOrGetChannel(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.Nearby);
            viewInstance!.CurrentChannelId = ChatChannel.NEARBY_CHANNEL_ID;
            chatHistory.AddMessage(ChatChannel.NEARBY_CHANNEL_ID, ChatMessage.NewFromSystem(WELCOME_MESSAGE));
            chatHistory.Channels[ChatChannel.NEARBY_CHANNEL_ID].MarkAllMessagesAsRead();
        }

        private void OnOpenConversation(string userId)
        {
            ChatChannel channel = chatHistory.AddOrGetChannel(new ChatChannel.ChannelId(userId), ChatChannel.ChatChannelType.User);
            viewInstance!.CurrentChannelId = channel.Id;
            viewInstance.FocusInputBox();
        }

        private void OnChatHistoryMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            bool isSentByOwnUser = addedMessage is { IsSystemMessage: false, IsSentByOwnUser: true };

            CreateChatBubble(destinationChannel, addedMessage, isSentByOwnUser);

            // If the chat is showing the channel that receives the message and the scroll view is at the bottom, mark everything as read
            if (viewInstance!.IsMessageListVisible && destinationChannel.Id.Equals(viewInstance.CurrentChannelId) && viewInstance.IsScrollAtBottom)
                MarkCurrentChannelAsRead();

            if (isSentByOwnUser)
            {
                MarkCurrentChannelAsRead();
                viewInstance.RefreshMessages();
                viewInstance.ShowLastMessage();
            }
            else
            {
                if (destinationChannel.Id.Equals(viewInstance.CurrentChannelId))
                {
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

        private async void OnViewCurrentChannelChangedAsync()
        {
            if (chatHistory.Channels[viewInstance!.CurrentChannelId].ChannelType == ChatChannel.ChatChannelType.User &&
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

        private void OnViewFoldingChanged(bool isUnfolded)
        {
            if (!isUnfolded)
                MarkCurrentChannelAsRead();
        }

        private void OnChatHistoryReadMessagesChanged(ChatChannel changedChannel)
        {
            if(changedChannel.Id.Equals(viewInstance.CurrentChannelId))
                viewInstance!.RefreshMessages();
            else
                viewInstance.RefreshUnreadMessages(changedChannel.Id);
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

        private async UniTask UpdateMembersDataAsync()
        {
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
                    viewInstance!.MemberCount = participantsCount;

                await UniTask.Delay(WAIT_TIME_IN_BETWEEN_UPDATES);
            }
        }

        protected override void OnBlur()
        {
            viewInstance!.DisableInputBoxSubmissions();
        }

        protected override void OnFocus()
        {
            if (viewInstance!.IsFocused) return;

            viewInstance.EnableInputBoxSubmissions();
        }

        protected override void OnViewShow()
        {
            cameraEntity = world.CacheCamera();

            viewInstance!.InjectDependencies(viewDependencies);
            viewInstance.Initialize(chatHistory.Channels, nametagsData.showChatBubbles, chatAudioSettings, GetProfilesFromParticipants, loadingStatus);
            chatStorage?.SetNewLocalUserWalletAddress(web3IdentityCache.Identity!.Address);

            //We start processing messages once the view is ready
            chatMessagesBus.MessageAdded += OnChatBusMessageAdded;
            chatHistory.MessageAdded += OnChatHistoryMessageAdded; // TODO: This should not exist, the only way to add a chat message from outside should be by using the bus
            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;
            chatCommandsBus.OnClearChat += Clear;

            chatEventBus.InsertTextInChat += OnEventTextInserted;
            chatEventBus.OpenConversation += OnOpenConversation;

            viewInstance.PointerEnter += OnViewPointerEnter;
            viewInstance.PointerExit += OnViewPointerExit;

            viewInstance.InputBoxFocusChanged += OnViewInputBoxFocusChanged;
            viewInstance.EmojiSelectionVisibilityChanged += OnViewEmojiSelectionVisibilityChanged;
            viewInstance.ChatBubbleVisibilityChanged += OnViewChatBubbleVisibilityChanged;
            viewInstance.InputSubmitted += OnViewInputSubmitted;
            viewInstance.MemberListVisibilityChanged += OnViewMemberListVisibilityChanged;
            viewInstance.ScrollBottomReached += OnViewScrollBottomReached;
            viewInstance.UnreadMessagesSeparatorViewed += OnViewUnreadMessagesSeparatorViewed;
            viewInstance.FoldingChanged += OnViewFoldingChanged;
            viewInstance.ChannelRemovalRequested += OnViewChannelRemovalRequested;
            viewInstance.CurrentChannelChanged += OnViewCurrentChannelChangedAsync;

            OnFocus();

            chatHistory.ChannelAdded += OnChatHistoryChannelAdded;
            chatHistory.ChannelRemoved += OnChatHistoryChannelRemoved;
            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;
            chatHistory.AllChannelsRemoved += OnChatHistoryAllChannelsRemoved;

            memberListCts = new CancellationTokenSource();
            UniTask.RunOnThreadPool(UpdateMembersDataAsync);
            ShowWelcomeMessage();

            chatStorage?.LoadAllChannelsWithoutMessages(); // TODO: Make it async?

            viewDependencies.DclInput.UI.Click.performed += OnUIClickPerformed;
            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed += OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed += OnOpenChatCommandLineShortcutPerformed;

            IsUnfolded = inputData.ShowUnfolded;
        }

        protected override void OnViewClose()
        {
            chatMessagesBus.MessageAdded -= OnChatBusMessageAdded;
            chatHistory.MessageAdded -= OnChatHistoryMessageAdded;
            chatHistory.ReadMessagesChanged -= OnChatHistoryReadMessagesChanged;
            chatCommandsBus.OnClearChat -= Clear;
            chatEventBus.InsertTextInChat -= OnEventTextInserted;

            if (viewInstance != null)
            {
                viewInstance.PointerEnter -= OnViewPointerEnter;
                viewInstance.PointerExit -= OnViewPointerExit;
                viewInstance.InputBoxFocusChanged -= OnViewInputBoxFocusChanged;
                viewInstance.EmojiSelectionVisibilityChanged -= OnViewEmojiSelectionVisibilityChanged;
                viewInstance.ChatBubbleVisibilityChanged -= OnViewChatBubbleVisibilityChanged;
                viewInstance.InputSubmitted -= OnViewInputSubmitted;
                viewInstance.ScrollBottomReached -= OnViewScrollBottomReached;
                viewInstance.UnreadMessagesSeparatorViewed -= OnViewUnreadMessagesSeparatorViewed;
                viewInstance.FoldingChanged -= OnViewFoldingChanged;
                viewInstance.MemberListVisibilityChanged -= OnViewMemberListVisibilityChanged;
                viewInstance.ChannelRemovalRequested -= OnViewChannelRemovalRequested;
                viewInstance.CurrentChannelChanged -= OnViewCurrentChannelChangedAsync;
                viewInstance.RemoveAllConversations();
                viewInstance.Dispose();
            }

            chatHistory.ChannelAdded -= OnChatHistoryChannelAdded;
            chatHistory.ChannelRemoved -= OnChatHistoryChannelRemoved;
            chatHistory.ReadMessagesChanged -= OnChatHistoryReadMessagesChanged;
            chatHistory.AllChannelsRemoved -= OnChatHistoryAllChannelsRemoved;

            viewDependencies.DclInput.UI.Click.performed -= OnUIClickPerformed;
            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed -= OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;
            viewDependencies.DclInput.UI.Submit.performed -= OnSubmitShortcutPerformed;

            viewDependencies.DclInput.UI.Click.performed -= OnUIClickPerformed;
            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed -= OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;

            Dispose();
        }

        private void MarkCurrentChannelAsRead()
        {
            chatHistory.Channels[viewInstance!.CurrentChannelId].MarkAllMessagesAsRead();
            messageCountWhenSeparatorViewed = chatHistory.Channels[viewInstance.CurrentChannelId].ReadMessages;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }

        private void CreateChatBubble(ChatChannel channel, ChatMessage chatMessage, bool isSentByOwnUser)
        {
            if (!nametagsData.showNameTags || !nametagsData.showChatBubbles)
                return;

            if (chatMessage.IsSentByOwnUser == false && entityParticipantTable.TryGet(chatMessage.SenderWalletAddress, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                Entity entity = entry.Entity;
                bool isPrivateMessage = channel.ChannelType == ChatChannel.ChatChannelType.User;
                GenerateChatBubbleComponent(entity, chatMessage, DEFAULT_COLOR, isPrivateMessage, channel.Id);

                switch (chatAudioSettings.chatAudioSettings)
                {
                    case ChatAudioSettings.NONE:
                        return;
                    case ChatAudioSettings.MENTIONS_ONLY when chatMessage.IsMention:
                    case ChatAudioSettings.ALL:
                        viewInstance!.PlayMessageReceivedSfx(chatMessage.IsMention);
                        break;
                }
            }
            else if (isSentByOwnUser)
            {
                if (channel.ChannelType == ChatChannel.ChatChannelType.User)
                {
                    if (!profileCache.TryGet(channel.Id.Id, out var profile))
                    {
                        GenerateChatBubbleComponent(playerEntity, chatMessage, DEFAULT_COLOR, true, channel.Id);
                    }
                    else
                    {
                        Color nameColor = profile!.UserNameColor != DEFAULT_COLOR? profile.UserNameColor : ProfileNameColorHelper.GetNameColor(profile.DisplayName);
                        GenerateChatBubbleComponent(playerEntity, chatMessage, nameColor, true, channel.Id, profile.ValidatedName, profile.WalletId);
                    }
                }
                else
                    GenerateChatBubbleComponent(playerEntity, chatMessage, DEFAULT_COLOR, false, channel.Id);
            }
        }

        private void GenerateChatBubbleComponent(Entity e, ChatMessage chatMessage, Color receiverNameColor, bool isPrivateMessage, ChatChannel.ChannelId messageChannelId, string? receiverDisplayName = null, string? receiverWalletId = null)
        {
            if (nametagsData is { showChatBubbles: true, showNameTags: true })
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

        private void OnViewChatBubbleVisibilityChanged(bool isVisible)
        {
            nametagsData.showChatBubbles = isVisible;

            ChatBubbleVisibilityChanged?.Invoke(isVisible);
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

        private void OnViewInputBoxFocusChanged(bool hasFocus)
        {
            if (hasFocus)
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
            viewInstance!.FocusInputBoxWithText("/");
        }

        private void OnToggleNametagsShortcutPerformed(InputAction.CallbackContext obj)
        {
            nametagsData.showNameTags = !nametagsData.showNameTags;
            viewInstance!.EnableChatBubblesVisibilityField = nametagsData.showNameTags;
        }

        private void OnUIClickPerformed(InputAction.CallbackContext obj)
        {
            viewInstance!.Click();
        }

        private void OnSubmitShortcutPerformed(InputAction.CallbackContext obj)
        {
            viewInstance!.FocusInputBox();
        }

        private void OnEventTextInserted(string text)
        {
            viewInstance!.FocusInputBox();
            viewInstance.InsertTextInInputBox(text);
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

        private void OnChatHistoryChannelRemoved(ChatChannel.ChannelId removedChannel)
        {
            viewInstance!.RemoveConversation(removedChannel);
        }

        private void OnChatHistoryChannelAdded(ChatChannel addedChannel)
        {
            viewInstance!.AddConversation(addedChannel);
        }
    }
}
