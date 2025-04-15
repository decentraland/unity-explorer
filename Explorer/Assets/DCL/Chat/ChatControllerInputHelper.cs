using Arch.Core;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using DCL.Nametags;
using ECS.Abstract;
using UnityEngine.InputSystem;
using Utility.Arch;

namespace DCL.Chat
{
    public class ChatControllerInputHelper
    {
        private readonly World world;
        private readonly IInputBlock inputBlock;
        private readonly IChatController chatController;
        private readonly SingleInstanceEntity cameraEntity;
        private readonly NametagsData nametagsData;
        private readonly IChatMessagesBus chatMessagesBus;

        public ChatControllerInputHelper(
            World world,
            IInputBlock inputBlock,
            IChatController chatController,
            SingleInstanceEntity cameraEntity,
            NametagsData nametagsData,
            IChatMessagesBus chatMessagesBus)
        {
            this.world = world;
            this.inputBlock = inputBlock;
            this.chatController = chatController;
            this.cameraEntity = cameraEntity;
            this.nametagsData = nametagsData;
            this.chatMessagesBus = chatMessagesBus;
        }

        public void DisableUnwantedInputs()
        {
            world.AddOrGet(cameraEntity, new CameraBlockerComponent());
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
        }

        public void EnableUnwantedInputs()
        {
            world.TryRemove<CameraBlockerComponent>(cameraEntity);
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
        }

        public void OnViewInputSubmitted(ChatChannel channel, string message, string origin)
        {
            chatMessagesBus.Send(channel, message, origin);
        }

        public void OnViewEmojiSelectionVisibilityChanged(bool isVisible)
        {
            if (isVisible)
                DisableUnwantedInputs();
            else
                EnableUnwantedInputs();
        }

        public void OnViewChatSelectStateChanged(bool isChatSelected)
        {
            if (isChatSelected)
                DisableUnwantedInputs();
            else
                EnableUnwantedInputs();
        }

        public void OnViewPointerExit() =>
            world.TryRemove<CameraBlockerComponent>(cameraEntity);

        public void OnViewPointerEnter() =>
            world.AddOrGet(cameraEntity, new CameraBlockerComponent());

        public void OnOpenChatCommandLineShortcutPerformed(InputAction.CallbackContext obj)
        {
            if (chatController.TryGetView(out var view))
                view.FocusInputBoxWithText("/");
        }

        public void OnTextInserted(string text)
        {
            if (chatController.TryGetView(out var view))
                view.InputBoxText = text;
        }

        public void OnToggleNametagsShortcutPerformed(InputAction.CallbackContext obj)
        {
            nametagsData.showNameTags = !nametagsData.showNameTags;
        }
    }
}
