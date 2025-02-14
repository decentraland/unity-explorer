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
using ECS.Abstract;
using LiveKit.Proto;
using LiveKit.Rooms;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.InputSystem;
using Utility;
using Utility.Arch;

namespace DCL.Chat
{
    public class ChatController : ControllerBase<ChatView>
    {
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
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

        private SingleInstanceEntity cameraEntity;
        private bool isMemberListInitialized;
        private CancellationTokenSource memberListCts;
        private string previousRoomSid = string.Empty;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public event Action<bool>? ChatBubbleVisibilityChanged;

        public ChatController(ViewFactoryMethod viewFactory,
            ChatEntryConfigurationSO chatEntryConfiguration,
            IChatMessagesBus chatMessagesBus,
            IChatHistory chatHistory,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            NametagsData nametagsData,
            World world,
            Entity playerEntity,
            IInputBlock inputBlock,
            ViewDependencies viewDependencies,
            IChatCommandsBus chatCommandsBus,
            IRoomHub roomHub) : base(viewFactory)
        {
            this.chatEntryConfiguration = chatEntryConfiguration;
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
        }

        public void Clear() // Called by a command
        {
            chatHistory.ClearChannel(viewInstance!.CurrentChannel);
            viewInstance!.RefreshMessages();
        }

        protected override void OnViewInstantiated()
        {
            cameraEntity = world.CacheCamera();

            //We start processing messages once the view is ready
            chatMessagesBus.MessageAdded += OnChatBusMessageAdded;
            chatCommandsBus.OnClearChat += Clear;
            chatHistory.MessageAdded += CreateChatEntry;

            viewInstance!.InjectDependencies(viewDependencies);
            viewInstance!.Initialize(chatHistory.Channels, ChatChannel.NEARBY_CHANNEL, nametagsData.showChatBubbles, chatEntryConfiguration);

            viewInstance.PointerEnter += OnChatViewPointerEnter;
            viewInstance.PointerExit += OnChatViewPointerExit;

            viewInstance.InputBoxFocusChanged += OnViewInputBoxFocusChanged;
            viewInstance.EmojiSelectionVisibilityChanged += OnViewEmojiSelectionVisibilityChanged;
            viewInstance.ChatBubbleVisibilityChanged += OnViewChatBubbleVisibilityChanged;
            viewInstance.InputSubmitted += OnViewInputSubmitted;
            viewInstance.MemberListVisibilityChanged += OnMemberListVisibilityChanged;

            OnFocus();

            // Intro message
            // TODO: Use localization systems here:
            chatHistory.AddMessage(ChatChannel.NEARBY_CHANNEL, ChatMessage.NewFromSystem("Type /help for available commands."));

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
        }

        public override void Dispose()
        {
            chatMessagesBus.MessageAdded -= OnChatBusMessageAdded;
            chatHistory.MessageAdded -= CreateChatEntry;
            chatCommandsBus.OnClearChat -= Clear;

            viewInstance!.PointerEnter -= OnChatViewPointerEnter;
            viewInstance.PointerExit -= OnChatViewPointerExit;

            viewInstance.InputBoxFocusChanged -= OnViewInputBoxFocusChanged;
            viewInstance.EmojiSelectionVisibilityChanged -= OnViewEmojiSelectionVisibilityChanged;
            viewInstance.ChatBubbleVisibilityChanged -= ChatBubbleVisibilityChanged;
            viewInstance.InputSubmitted -= OnViewInputSubmitted;
            viewInstance.MemberListVisibilityChanged -= OnMemberListVisibilityChanged;

            viewDependencies.DclInput.UI.Click.performed -= OnUIClickPerformed;
            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed -= OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChat.performed -= OnOpenChatShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;
            viewDependencies.DclInput.UI.Submit.performed -= OnSubmitShorcutPerformed;

            memberListCts.SafeCancelAndDispose();

            viewInstance.Dispose();
        }

        private bool canUpdateParticipants => islandRoom.Info.ConnectionState == ConnectionState.ConnConnected;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        private void CreateChatEntry(ChatChannel channel, ChatMessage chatMessage)
        {
            // Chat bubble over the avatars
            if (chatMessage.SentByOwnUser == false && entityParticipantTable.TryGet(chatMessage.WalletAddress, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                Entity entity = entry.Entity;
                GenerateChatBubbleComponent(entity, chatMessage);
                viewInstance!.PlayMessageReceivedSfx();
            }
            else if (chatMessage is { SystemMessage: false, SentByOwnUser: true })
                GenerateChatBubbleComponent(playerEntity, chatMessage);

            // New entry in the chat window
            viewInstance!.RefreshMessages();
        }

        private void GenerateChatBubbleComponent(Entity e, ChatMessage chatMessage)
        {
            if (nametagsData is { showChatBubbles: true, showNameTags: true })
                world.AddOrGet(e, new ChatBubbleComponent(chatMessage.Message, chatMessage.SenderValidatedName, chatMessage.WalletAddress));
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
            nametagsData.showNameTags = !nametagsData.showNameTags;
            viewInstance!.EnableChatBubblesVisibilityField = nametagsData.showNameTags;
        }

        private void OnUIClickPerformed(InputAction.CallbackContext obj)
        {
            viewInstance!.Click();
        }

        private void OnSubmitShorcutPerformed(InputAction.CallbackContext obj)
        {
            viewInstance!.FocusInputBox();
        }

        private void OnChatBusMessageAdded(ChatChannel.ChannelId channelId, ChatMessage chatMessage)
        {
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
            Dictionary<string, ChatMemberListView.MemberData> members = new Dictionary<string, ChatMemberListView.MemberData>();

            // Island room
            IReadOnlyCollection<string> islandIdentities = islandRoom.Participants.RemoteParticipantIdentities();

            foreach (string identity in islandIdentities)
            {
                ChatMemberListView.MemberData newMember = GetMemberDataFromParticipantIdentity(identity);

                if (!string.IsNullOrEmpty(newMember.Name))
                    members.Add(identity, newMember);
            }

            return members;
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
                newMemberData.ProfileColor = chatEntryConfiguration.GetNameColor(profile.ValidatedName);
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
