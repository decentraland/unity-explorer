using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.ChatLifecycleBus;
using DCL.Chat.EventBus;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.Profiles;
using DCL.Settings.Settings;
using DCL.UI.InputFieldFormatting;
using ECS.Abstract;
using LiveKit.Proto;
using LiveKit.Rooms;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.InputSystem;
using Utility;
using Utility.Arch;

namespace DCL.Chat
{
    public class ChatController : ControllerBase<ChatView>
    {
        public delegate void ChatBubbleVisibilityChangedDelegate(bool isVisible);

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
        private readonly IRoomHub roomHub;

        private SingleInstanceEntity cameraEntity;
        private CancellationTokenSource memberListCts;
        private string previousRoomSid = string.Empty;
        private readonly List<ChatMemberListView.MemberData> membersBuffer = new ();
        private readonly List<Profile> participantProfileBuffer = new ();

        // Used exclusively to calculate the new value of the read messages once the Unread messages separator has been viewed
        private int messageCountWhenSeparatorViewed;
        private bool hasToResetUnreadMessagesWhenNewMessageArrive;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        private bool canUpdateParticipants => islandRoom.Info.ConnectionState == ConnectionState.ConnConnected;

        public event ChatBubbleVisibilityChangedDelegate? ChatBubbleVisibilityChanged;

        public ChatController(
            ViewFactoryMethod viewFactory,
            IChatMessagesBus chatMessagesBus,
            IChatHistory chatHistory,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            NametagsData nametagsData,
            World world,
            Entity playerEntity,
            IChatLifecycleBusController chatLifecycleBusController,
            IInputBlock inputBlock,
            ViewDependencies viewDependencies,
            IChatCommandsBus chatCommandsBus,
            IRoomHub roomHub,
            ChatAudioSettingsAsset chatAudioSettings,
            ITextFormatter hyperlinkTextFormatter,
            IProfileCache profileCache, IChatEventBus chatEventBus) : base(viewFactory)
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
            this.roomHub = roomHub;
            this.islandRoom = roomHub.IslandRoom();
            this.chatAudioSettings = chatAudioSettings;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
            this.profileCache = profileCache;
            this.chatEventBus = chatEventBus;
            chatLifecycleBusController.SubscribeToHideChatCommand(HideBusCommandReceived);
        }

        public void Clear() // Called by a command
        {
            chatHistory.ClearChannel(viewInstance!.CurrentChannelId);
            messageCountWhenSeparatorViewed = 0;
        }

        public override void Dispose()
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
                viewInstance.Dispose();
            }

            viewDependencies.DclInput.UI.Click.performed -= OnUIClickPerformed;
            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed -= OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChat.performed -= OnOpenChatShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;
            viewDependencies.DclInput.UI.Submit.performed -= OnSubmitShortcutPerformed;

            memberListCts.SafeCancelAndDispose();
        }

        private void HideBusCommandReceived()
        {
            HideViewAsync(CancellationToken.None).Forget();
        }

        protected override void OnViewInstantiated()
        {
            cameraEntity = world.CacheCamera();

            //We start processing messages once the view is ready
            chatMessagesBus.MessageAdded += OnChatBusMessageAdded;
            chatHistory.MessageAdded += OnChatHistoryMessageAdded; // TODO: This should not exist, the only way to add a chat message from outside should be by using the bus
            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;
            chatCommandsBus.OnClearChat += Clear;

            chatEventBus.InsertTextInChat += OnEventTextInserted;
            chatEventBus.OpenConversation += OnOpenConversation;

            viewInstance!.InjectDependencies(viewDependencies);
            viewInstance!.Initialize(chatHistory.Channels, nametagsData.showChatBubbles, chatAudioSettings, GetProfilesFromParticipants);

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

            OnFocus();

            chatHistory.AddMessage(ChatChannel.NEARBY_CHANNEL_ID, ChatMessage.NewWelcomeMessage());
            chatHistory.Channels[ChatChannel.NEARBY_CHANNEL_ID].MarkAllMessagesAsRead();

            chatHistory.ChannelAdded += OnChatHistoryChannelAdded;
            chatHistory.ChannelRemoved += OnChatHistoryChannelRemoved;
            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;

            memberListCts = new CancellationTokenSource();
            UniTask.RunOnThreadPool(UpdateMembersDataAsync);
        }

        private void OnOpenConversation(string userId)
        {
            var channel = chatHistory.AddOrGetChannel(ChatChannel.ChatChannelType.User, new ChatChannel.ChannelId(userId));
            viewInstance!.CurrentChannelId = channel.Id;
            viewInstance.FocusInputBox();
        }

        private void OnChatHistoryMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            bool isSentByOwnUser = addedMessage is { SystemMessage: false, SentByOwnUser: true };

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

        private void OnViewChannelRemovalRequested(ChatChannel.ChannelId channelId)
        {
            chatHistory.RemoveChannel(channelId);
            viewInstance.RemoveConversation(channelId);
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
                // If the player jumps to another room (like a world) while the member list is visible, it must refresh
                if (previousRoomSid != islandRoom.Info.Sid && viewInstance!.IsMemberListVisible)
                {
                    previousRoomSid = islandRoom.Info.Sid;
                    RefreshMemberList();
                }

                // Updates the amount of members
                if(canUpdateParticipants && islandRoom.Participants.RemoteParticipantIdentities().Count != viewInstance!.MemberCount)
                    viewInstance!.MemberCount = islandRoom.Participants.RemoteParticipantIdentities().Count;

                await UniTask.Delay(WAIT_TIME_IN_BETWEEN_UPDATES);
            }
        }

        protected override void OnBlur()
        {
            viewDependencies.DclInput.UI.Submit.performed -= OnSubmitShortcutPerformed;
            viewInstance!.DisableInputBoxSubmissions();
        }

        protected override void OnFocus()
        {
            if (viewInstance!.IsFocused) return;

            viewInstance.EnableInputBoxSubmissions();
            viewDependencies.DclInput.UI.Submit.performed += OnSubmitShortcutPerformed;
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            viewDependencies.DclInput.UI.Click.performed += OnUIClickPerformed;
            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed += OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChat.performed += OnOpenChatShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed += OnOpenChatCommandLineShortcutPerformed;
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            viewDependencies.DclInput.UI.Click.performed -= OnUIClickPerformed;
            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed -= OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChat.performed -= OnOpenChatShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;

            MarkCurrentChannelAsRead();
        }

        private void MarkCurrentChannelAsRead()
        {
            chatHistory.Channels[viewInstance!.CurrentChannelId].MarkAllMessagesAsRead();
            messageCountWhenSeparatorViewed = chatHistory.Channels[viewInstance.CurrentChannelId].ReadMessages;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        private void CreateChatBubble(ChatChannel channel, ChatMessage chatMessage, bool isSentByOwnUser)
        {
            // Chat bubble over the avatars
            if (chatMessage.SentByOwnUser == false && entityParticipantTable.TryGet(chatMessage.WalletAddress, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                Entity entity = entry.Entity;
                GenerateChatBubbleComponent(entity, chatMessage);

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
                GenerateChatBubbleComponent(playerEntity, chatMessage);
        }

        private void GenerateChatBubbleComponent(Entity e, ChatMessage chatMessage)
        {
            if (nametagsData is { showChatBubbles: true, showNameTags: true })
            {
                bool isPrivateMessage = viewInstance!.CurrentChannelId.Equals(ChatChannel.NEARBY_CHANNEL_ID);
                world.AddOrSet(e, new ChatBubbleComponent(chatMessage.Message, chatMessage.SenderValidatedName, chatMessage.WalletAddress, chatMessage.IsMention, isPrivateMessage));
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

        private void OnOpenChatShortcutPerformed(InputAction.CallbackContext obj)
        {
            viewInstance!.FocusInputBoxWithText(string.Empty);
        }

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
            if (!chatMessage.SystemMessage)
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
            if (isVisible && canUpdateParticipants)
            {
                RefreshMemberList();
            }
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

            // Island room
            IReadOnlyCollection<string> islandIdentities = islandRoom.Participants.RemoteParticipantIdentities();

            foreach (string identity in islandIdentities)
            {
                Profile profile = profileCache.Get(identity);

                if(profile != null)
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
