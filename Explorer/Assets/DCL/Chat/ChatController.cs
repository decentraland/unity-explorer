using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
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
        private readonly IRoom sceneRoom;

        private readonly ITextFormatter hyperlinkTextFormatter;
        private readonly ChatAudioSettingsAsset chatAudioSettings;
        private SingleInstanceEntity cameraEntity;
        private bool isMemberListInitialized;

        private CancellationTokenSource memberListCts;
        private string previousRoomSid = string.Empty;
        private readonly Dictionary<string, ChatMemberListView.MemberData> membersBuffer = new ();

        // Used exclusively to calculate the new value of the read messages once the Unread messages separator has been viewed
        private int messageCountWhenSeparatorViewed;
        private bool hasToResetUnreadMessagesWhenNewMessageArrive;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public event ChatBubbleVisibilityChangedDelegate? ChatBubbleVisibilityChanged;

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
            ChatAudioSettingsAsset chatAudioSettings, ITextFormatter hyperlinkTextFormatter) : base(viewFactory)
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
            this.sceneRoom = roomHub.SceneRoom().Room();
            this.chatAudioSettings = chatAudioSettings;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
        }

        public void Clear() // Called by a command
        {
            chatHistory.ClearChannel(viewInstance!.CurrentChannel);
            messageCountWhenSeparatorViewed = 0;
        }

        public override void Dispose()
        {
            chatMessagesBus.MessageAdded -= OnChatBusMessageAdded;
            chatHistory.MessageAdded -= OnChatHistoryMessageAdded;
            chatHistory.ReadMessagesChanged -= OnChatHistoryReadMessagesChanged;
            chatCommandsBus.OnClearChat -= Clear;

            viewInstance!.PointerEnter -= OnChatViewPointerEnter;
            viewInstance.PointerExit -= OnChatViewPointerExit;

            viewInstance.InputBoxFocusChanged -= OnViewInputBoxFocusChanged;
            viewInstance.EmojiSelectionVisibilityChanged -= OnViewEmojiSelectionVisibilityChanged;
            viewInstance.ChatBubbleVisibilityChanged -= OnViewChatBubbleVisibilityChanged;
            viewInstance.InputSubmitted -= OnViewInputSubmitted;
            viewInstance.ScrollBottomReached -= OnViewScrollBottomReached;
            viewInstance.UnreadMessagesSeparatorViewed -= OnViewUnreadMessagesSeparatorViewed;
            viewInstance.FoldingChanged -= OnViewFoldingChanged;
            viewInstance.MemberListVisibilityChanged -= OnMemberListVisibilityChanged;

            viewDependencies.DclInput.UI.Click.performed -= OnUIClickPerformed;
            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed -= OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChat.performed -= OnOpenChatShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;
            viewDependencies.DclInput.UI.Submit.performed -= OnSubmitShorcutPerformed;

            memberListCts.SafeCancelAndDispose();

            viewInstance.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            cameraEntity = world.CacheCamera();

            //We start processing messages once the view is ready
            chatMessagesBus.MessageAdded += OnChatBusMessageAdded;
            chatHistory.MessageAdded += OnChatHistoryMessageAdded; // TODO: This should not exist, the only way to add a chat message from outside should be by using the bus
            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;
            chatCommandsBus.OnClearChat += Clear;

            viewInstance!.InjectDependencies(viewDependencies);
            viewInstance!.Initialize(chatHistory.Channels, ChatChannel.NEARBY_CHANNEL, nametagsData.showChatBubbles, chatAudioSettings);

            viewInstance.PointerEnter += OnChatViewPointerEnter;
            viewInstance.PointerExit += OnChatViewPointerExit;

            viewInstance.InputBoxFocusChanged += OnViewInputBoxFocusChanged;
            viewInstance.EmojiSelectionVisibilityChanged += OnViewEmojiSelectionVisibilityChanged;
            viewInstance.ChatBubbleVisibilityChanged += OnViewChatBubbleVisibilityChanged;
            viewInstance.InputSubmitted += OnViewInputSubmitted;
            viewInstance.MemberListVisibilityChanged += OnMemberListVisibilityChanged;
            viewInstance.ScrollBottomReached += OnViewScrollBottomReached;
            viewInstance.UnreadMessagesSeparatorViewed += OnViewUnreadMessagesSeparatorViewed;
            viewInstance.FoldingChanged += OnViewFoldingChanged;

            OnFocus();

            // Intro message
            // TODO: Use localization systems here:
            chatHistory.AddMessage(ChatChannel.NEARBY_CHANNEL, ChatMessage.NewFromSystem("Type /help for available commands."));

            memberListCts = new CancellationTokenSource();
            UniTask.RunOnThreadPool(UpdateMembersData);

            //            ChatChannel.ChannelId id = chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "USER1");
            //            chatHistory.AddMessage(id, new ChatMessage("USER1", "user", "", false, false, "", true));
            //            id = chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "USER2");
            //            chatHistory.AddMessage(id, new ChatMessage("USER2", "user", "", false, false, "", true));
            //            id = chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "USER3");
            //            chatHistory.AddMessage(id, new ChatMessage("USER3", "user", "", false, false, "", true));
            //            id = chatHistory.AddChannel(ChatChannel.ChatChannelType.User, "USER4");
            //            chatHistory.AddMessage(id, new ChatMessage("USER4", "user", "", false, false, "", true));
        }

        //        private int current = 0;
        //       private ChatChannel.ChannelId[] ids = new []
        //       {
        //           ChatChannel.NEARBY_CHANNEL,
        //        new ChatChannel.ChannelId(ChatChannel.ChatChannelType.User, "USER1"),
        //        new ChatChannel.ChannelId(ChatChannel.ChatChannelType.User, "USER2"),
        //        new ChatChannel.ChannelId(ChatChannel.ChatChannelType.User, "USER3"),
        //        new ChatChannel.ChannelId(ChatChannel.ChatChannelType.User, "USER4")
        //     };

        private void OnChatHistoryMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            bool isSentByOwnUser = addedMessage is { SystemMessage: false, SentByOwnUser: true };

            CreateChatBubble(destinationChannel, addedMessage, isSentByOwnUser);

            // If the chat is showing the channel that receives the message and the scroll view is at the bottom, mark everything as read
            if (viewInstance!.IsUnfolded && destinationChannel.Id.Equals(viewInstance.CurrentChannel) && viewInstance.IsScrollAtBottom)
                MarkCurrentChannelAsRead();

            if (isSentByOwnUser)
            {
                MarkCurrentChannelAsRead();
                viewInstance.RefreshMessages();
                viewInstance.ShowLastMessage();
            }
            else
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
        }

        private void OnViewFoldingChanged(bool isUnfolded)
        {
            if (!isUnfolded)
                MarkCurrentChannelAsRead();
        }

        private void OnChatHistoryReadMessagesChanged(ChatChannel changedChannel)
        {
            viewInstance!.RefreshMessages();
        }

        private void OnViewUnreadMessagesSeparatorViewed()
        {
            messageCountWhenSeparatorViewed = chatHistory.Channels[viewInstance!.CurrentChannel].Messages.Count;
            hasToResetUnreadMessagesWhenNewMessageArrive = true;
        }

        private void OnViewScrollBottomReached()
        {
            MarkCurrentChannelAsRead();

            memberListCts = new CancellationTokenSource();
            UniTask.RunOnThreadPool(UpdateMembersData);
        }

        private async UniTask UpdateMembersData()
        {
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

                await UniTask.Delay(500);
            }
        }

        protected override void OnBlur()
        {
            viewDependencies.DclInput.UI.Submit.performed -= OnSubmitShorcutPerformed;
            viewInstance!.DisableInputBoxSubmissions();
        }

        protected override void OnFocus()
        {
            viewInstance!.EnableInputBoxSubmissions();
            viewDependencies.DclInput.UI.Submit.performed += OnSubmitShorcutPerformed;
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
            chatHistory.Channels[viewInstance!.CurrentChannel].MarkAllMessagesAsRead();
            messageCountWhenSeparatorViewed = chatHistory.Channels[viewInstance.CurrentChannel].ReadMessages;
        }

        private bool canUpdateParticipants => islandRoom.Info.ConnectionState == ConnectionState.ConnConnected;

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
                world.AddOrGet(e, new ChatBubbleComponent(chatMessage.Message, chatMessage.SenderValidatedName, chatMessage.WalletAddress, chatMessage.IsMention));
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

        private void OnChatViewPointerExit() =>
            world.TryRemove<CameraBlockerComponent>(cameraEntity);

        private void OnChatViewPointerEnter() =>
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
            //            chatHistory.AddMessage(viewInstance!.CurrentChannel, new ChatMessage("NEW!", "Test", "", false, "", true));
            nametagsData.showNameTags = !nametagsData.showNameTags;
            viewInstance!.EnableChatBubblesVisibilityField = nametagsData.showNameTags;
        }

        private void OnUIClickPerformed(InputAction.CallbackContext obj)
        {
            //            current = (current + 1) % chatHistory.Channels.Count;
            //            viewInstance.CurrentChannel = ids[current];
            viewInstance!.Click();
        }

        private void OnSubmitShorcutPerformed(InputAction.CallbackContext obj)
        {
            viewInstance!.FocusInputBox();
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

        private void OnMemberListVisibilityChanged(bool isVisible)
        {
            if (isVisible && canUpdateParticipants)
            {
                RefreshMemberList();
            }
        }

        private Dictionary<string, ChatMemberListView.MemberData> GenerateMemberList()
        {
            membersBuffer.Clear();

            // Island room
            IReadOnlyCollection<string> islandIdentities = islandRoom.Participants.RemoteParticipantIdentities();

            foreach (string identity in islandIdentities)
            {
                ChatMemberListView.MemberData newMember = GetMemberDataFromParticipantIdentity(identity);

                if (!string.IsNullOrEmpty(newMember.Name))
                    membersBuffer.Add(identity, newMember);
            }

            return membersBuffer;
        }

        private ChatMemberListView.MemberData GetMemberDataFromParticipantIdentity(string identity)
        {
            Profile profile = viewDependencies.ProfileCache.Get(identity);

            ChatMemberListView.MemberData newMemberData = new ChatMemberListView.MemberData();
            newMemberData.Id = identity;

            if (profile != null)
            {
                newMemberData.Name = profile.ValidatedName;
                newMemberData.ProfilePicture = profile.ProfilePicture.Value.Asset.Sprite;
                newMemberData.ConnectionStatus = ChatMemberConnectionStatus.Online; // TODO: Get this info from somewhere, when the other shapes are developed
                newMemberData.WalletId = profile.WalletId;
                newMemberData.ProfileColor = viewDependencies.ProfileNameColorHelper.GetNameColor(profile.ValidatedName);
            }

            return newMemberData;
        }

        private void RefreshMemberList()
        {
            Dictionary<string, ChatMemberListView.MemberData> members = GenerateMemberList();
            viewInstance!.SetMemberData(members);
        }

    }
}
