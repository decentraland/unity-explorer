using DCL.Audio;
using DCL.Emoji;
using DCL.UI.CustomInputField;
using MVC;

namespace DCL.Chat
{
    public class EmojiPanelChatInputState : IndependentMVCState<ChatInputStateContext>
    {
        private readonly EmojiPanelController emojiPanelController;
        private readonly ChatInputView.EmojiContainer emojiContainer;
        private readonly CustomInputField inputField;
        private readonly ChatClickDetectionService clickDetectionService;

        public EmojiPanelChatInputState(ChatInputStateContext context) : base(context)
        {
            emojiContainer = context.ChatInputView.emojiContainer;

            emojiPanelController = new EmojiPanelController(
                emojiContainer.emojiPanel,
                emojiContainer.emojiPanelConfiguration,
                context.EmojiMapping,
                emojiContainer.emojiSectionViewPrefab,
                emojiContainer.emojiButtonPrefab
            );

            inputField = context.ChatInputView.inputField;

            clickDetectionService = new ChatClickDetectionService(emojiContainer.emojiPanel.transform);
        }

        protected override void Activate(ControllerNoData input)
        {
            emojiPanelController.SetPanelVisibility(true);
            emojiContainer.emojiPanelButton.SetState(true);
            emojiContainer.emojiPanel.EmojiContainer.gameObject.SetActive(true);
            emojiPanelController.EmojiSelected += OnEmojiSelected;
            clickDetectionService.OnClickOutside += Deactivate;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(emojiContainer.openEmojiPanelAudio);
        }

        protected override void Deactivate()
        {
            emojiPanelController.SetPanelVisibility(false);
            emojiContainer.emojiPanelButton.SetState(false);
            emojiContainer.emojiPanel.EmojiContainer.gameObject.SetActive(false);
            emojiPanelController.EmojiSelected -= OnEmojiSelected;
            clickDetectionService.OnClickOutside -= Deactivate;
        }

        private void OnEmojiSelected(string emoji)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(emojiContainer.addEmojiAudio);
            if (!inputField.IsWithinCharacterLimit(emoji.Length)) return;
            inputField.InsertTextAtCaretPosition(emoji);
        }
    }
}
