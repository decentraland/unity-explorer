using Arch.Core;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using ECS.Abstract;
using Utility.Arch;

namespace DCL.Chat
{
    public class ChatControllerInputHelper
    {
        private readonly World world;
        private readonly IInputBlock inputBlock;
        private readonly IChatController chatController;
        private readonly SingleInstanceEntity cameraEntity;

        public ChatControllerInputHelper(
            World world,
            IInputBlock inputBlock,
            IChatController chatController,
            SingleInstanceEntity cameraEntity)
        {
            this.world = world;
            this.inputBlock = inputBlock;
            this.chatController = chatController;
            this.cameraEntity = cameraEntity;
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

        public void OnTextInserted(string text)
        {
            if (chatController.TryGetView(out var view))
            {
                if (view.IsMaskActive) return;
                view.FocusInputBox();
                view.InsertTextInInputBox(text);
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
    }
}
