using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using ECS.Abstract;
using MVC;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine.InputSystem;
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

        private SingleInstanceEntity cameraEntity;
        private (IChatCommand command, Match param) chatCommand;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public event Action<bool>? ChatBubbleVisibilityChanged;

        public ChatController(
            ViewFactoryMethod viewFactory,
            ChatEntryConfigurationSO chatEntryConfiguration,
            IChatMessagesBus chatMessagesBus,
            IChatHistory chatHistory,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            NametagsData nametagsData,
            World world,
            Entity playerEntity,
            IInputBlock inputBlock,
            ViewDependencies viewDependencies
        ) : base(viewFactory)
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
            chatHistory.MessageAdded += CreateChatEntry; // TODO: This should not exist, the only way to add a chat message from outside should be by using the bus

            viewInstance!.InjectDependencies(viewDependencies);
            viewInstance!.Initialize(chatHistory.Channels, ChatChannel.NEARBY_CHANNEL, nametagsData.showChatBubbles, chatEntryConfiguration);

            viewInstance.PointerEnter += OnChatViewPointerEnter;
            viewInstance.PointerExit += OnChatViewPointerExit;

            viewInstance.InputBoxFocusChanged += OnViewInputBoxFocusChanged;
            viewInstance.EmojiSelectionVisibilityChanged += OnViewEmojiSelectionVisibilityChanged;
            viewInstance.ChatBubbleVisibilityChanged += OnViewChatBubbleVisibilityChanged;
            viewInstance.InputSubmitted += OnViewInputSubmitted;
            viewInstance.ScrollBottomReached += OnViewScrollBottomReached;

            OnFocus();

            // Intro message
            // TODO: Use localization systems here:
            chatHistory.AddMessage(ChatChannel.NEARBY_CHANNEL, ChatMessage.NewFromSystem("Type /help for available commands."));

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

        private void OnViewScrollBottomReached()
        {
            chatHistory.Channels[viewInstance.CurrentChannel]!.MarkAllMessagesAsRead();
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

            chatHistory.Channels[viewInstance!.CurrentChannel].MarkAllMessagesAsRead();
        }

        public override void Dispose()
        {
            chatMessagesBus.MessageAdded -= OnChatBusMessageAdded;
            chatHistory.MessageAdded -= CreateChatEntry;

            viewInstance!.PointerEnter -= OnChatViewPointerEnter;
            viewInstance.PointerExit -= OnChatViewPointerExit;

            viewInstance.InputBoxFocusChanged -= OnViewInputBoxFocusChanged;
            viewInstance.EmojiSelectionVisibilityChanged -= OnViewEmojiSelectionVisibilityChanged;
            viewInstance.ChatBubbleVisibilityChanged -= ChatBubbleVisibilityChanged;
            viewInstance.InputSubmitted -= OnViewInputSubmitted;
            viewInstance.ScrollBottomReached -= OnViewScrollBottomReached;

            viewDependencies.DclInput.UI.Click.performed -= OnUIClickPerformed;
            viewDependencies.DclInput.Shortcuts.ToggleNametags.performed -= OnToggleNametagsShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChat.performed -= OnOpenChatShortcutPerformed;
            viewDependencies.DclInput.Shortcuts.OpenChatCommandLine.performed -= OnOpenChatCommandLineShortcutPerformed;
            viewDependencies.DclInput.UI.Submit.performed -= OnSubmitShorcutPerformed;

            viewInstance.Dispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        private void CreateChatEntry(ChatChannel channel, ChatMessage chatMessage)
        {
            bool isSentByOwnUser = chatMessage is { SystemMessage: false, SentByOwnUser: true };

            // Chat bubble over the avatars
            if (chatMessage.SentByOwnUser == false && entityParticipantTable.TryGet(chatMessage.WalletAddress, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                Entity entity = entry.Entity;
                GenerateChatBubbleComponent(entity, chatMessage);
                viewInstance!.PlayMessageReceivedSfx();
            }
            else if (isSentByOwnUser)
                GenerateChatBubbleComponent(playerEntity, chatMessage);

            if (viewInstance!.IsScrollAtBottom || isSentByOwnUser)
                chatHistory.Channels[viewInstance.CurrentChannel].MarkAllMessagesAsRead();

            // New entry in the chat window
            viewInstance!.RefreshMessages();

            if(isSentByOwnUser)
                viewInstance!.ShowLastMessage();
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
            chatHistory.AddMessage(channelId, chatMessage);
        }
    }
}
