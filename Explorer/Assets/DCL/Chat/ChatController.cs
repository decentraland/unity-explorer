using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.ChatLifecycleBus;
using DCL.Chat.InputBus;
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
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;
using Utility.Arch;
using Random = UnityEngine.Random;

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
        private readonly IProfileCache profileCache;
        private readonly ITextFormatter hyperlinkTextFormatter;
        private readonly ChatAudioSettingsAsset chatAudioSettings;
        private readonly IChatInputBus chatInputBus;
        private readonly ChatStorage chatStorage;

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
            IProfileCache profileCache,
            IChatInputBus chatInputBus,
            ChatStorage chatStorage) : base(viewFactory)
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
            this.chatAudioSettings = chatAudioSettings;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
            this.profileCache = profileCache;
            this.chatInputBus = chatInputBus;
            chatLifecycleBusController.SubscribeToHideChatCommand(HideBusCommandReceived);
            this.chatStorage = chatStorage;
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
            chatInputBus.InsertTextInChat -= OnInputTextInserted;

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
                viewInstance.CurrentChannelChanged -= OnViewCurrentChannelChanged;
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
            chatInputBus.InsertTextInChat += OnInputTextInserted;

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
            viewInstance.CurrentChannelChanged += OnViewCurrentChannelChanged;

            OnFocus();

            // Intro message
            // TODO: Use localization systems here:
            chatHistory.AddMessage(ChatChannel.NEARBY_CHANNEL, ChatMessage.NewFromSystem("Type /help for available commands."));
            chatHistory.Channels[ChatChannel.NEARBY_CHANNEL].MarkAllMessagesAsRead();

            chatHistory.ChannelAdded += OnChatHistoryChannelAdded;
            chatHistory.ChannelRemoved += OnChatHistoryChannelRemoved;
            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;

// TODO: REMOVE ALL THESE LINES AFTER TESTING
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0x024b912f2c35cebc1e2b06987baa2b1280a8291d");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0xc9C29AB98E6BC42015985165A11153F564e9F8C2");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0x51a514d3F28Ea19775e811fC09396E808394bd12");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0xcd4ea8e05945f34122679f5035cd6014f3263863");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0x6A327965bE29a7AcB83E1d1bbD689B72E188E58d");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0xd545B9E0A5F3638a5026d1914CC9b47ed16B5ae9");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0x69D30b1875d39E13A01AF73CCFED6d84839e84f2");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0x8e41609eD5e365Ac23C28d9625Bd936EA9C9E22c");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0x97574fCd296f73FE34823973390ebE4b9b065300");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0x31d4f4DD8615ec45bbB6330DA69F60032Aca219E");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0xd0bBE281840cF1ccEBF202e547b539a94e2e9DA3");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0x1BB3CeCd07DE9A8456cD3d6076b87c7a546162d0");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0x04E77bA608Cc78aD8aEFfBc60a2Ea47ABdaEA7BA");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0xe2b6024873d218B2E83B462D3658D8D7C3f55a18");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0x1b8BA74cC34C2927aac0a8AF9C3B1BA2e61352F2");
chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "0xdA5462CDb7091c39dE8cC0dE49e96632ED33197A");

viewDependencies.DclInput.TESTS.Action1.performed += (x) => { chatHistory.AddMessage(ChatChannel.NEARBY_CHANNEL, new ChatMessage("Test1 " + Random.Range(0, 100), "Test1", "Address", false, "senderID", false, false)); };
viewDependencies.DclInput.TESTS.Action2.performed += (x) => { chatHistory.AddMessage(new ChatChannel.ChannelId(ChatChannel.ChatChannelType.User, "0x024b912f2c35cebc1e2b06987baa2b1280a8291d"), new ChatMessage("Test2 " + Random.Range(0, 100), "Test2", "0x024b912f2c35cebc1e2b06987baa2b1280a8291d", false, "senderID", false, false)); };
viewDependencies.DclInput.TESTS.Action3.performed += (x) => { chatHistory.AddMessage(new ChatChannel.ChannelId(ChatChannel.ChatChannelType.User, "0xc9C29AB98E6BC42015985165A11153F564e9F8C2"), new ChatMessage("Test3 " + Random.Range(0, 100), "Test3", "0xc9C29AB98E6BC42015985165A11153F564e9F8C2", false, "senderID", false, false)); };

            memberListCts = new CancellationTokenSource();
            UniTask.RunOnThreadPool(UpdateMembersDataAsync);

            chatStorage.LoadAllChannelsWithoutMessages(); // TODO: Make it async?
            // TODO: Load messages when entering a conversation
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

        private void OnViewCurrentChannelChanged()
        {
            if(!chatStorage.IsChannelInitialized(viewInstance.CurrentChannelId))
                chatStorage.InitializeChannelWithMessages(viewInstance.CurrentChannelId);
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
            if (chatMessage.SentByOwnUser == false && entityParticipantTable.TryGet(chatMessage.SenderWalletAddress, out IReadOnlyEntityParticipantTable.Entry entry))
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
                world.AddOrGet(e, new ChatBubbleComponent(chatMessage.Message, chatMessage.SenderValidatedName, chatMessage.SenderWalletAddress, chatMessage.IsMention));
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
            chatMessagesBus.Send(channel.Id, message, origin);
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

        private void OnInputTextInserted(string text)
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
