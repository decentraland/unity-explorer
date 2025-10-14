using DCL.Audio;
using DCL.Chat.ChatServices;
using DCL.Emoji;
using DCL.UI.CustomInputField;
using MVC;
using System;

namespace DCL.Chat.ChatInput
{
    public class EmojiPanelChatInputState : IndependentMVCState<ChatInputStateContext>, IDisposable
    {
        private readonly EmojiPanelPresenter emojiPanelPresenter;
        private readonly ChatInputView.EmojiContainer emojiContainer;
        private readonly CustomInputField inputField;
        private readonly ChatClickDetectionService clickDetectionService;

        public EmojiPanelChatInputState(ChatInputStateContext context) : base(context)
        {
            emojiContainer = context.ChatInputView.emojiContainer;

            emojiPanelPresenter = new EmojiPanelPresenter(
                emojiContainer.emojiPanel,
                emojiContainer.emojiPanelConfiguration,
                context.EmojiMapping,
                emojiContainer.emojiSectionViewPrefab,
                emojiContainer.emojiButtonPrefab
            );

            inputField = context.ChatInputView.inputField;

            clickDetectionService = new ChatClickDetectionService(emojiContainer.emojiPanel.transform);
            clickDetectionService.OnClickOutside += Deactivate;
            clickDetectionService.Pause();
        }

        protected override void Activate(ControllerNoData input)
        {
            emojiPanelPresenter.SetPanelVisibility(true);
            emojiContainer.emojiPanelButton.SetState(true);
            emojiContainer.emojiPanel.EmojiContainer.gameObject.SetActive(true);
            emojiPanelPresenter.EmojiSelected += OnEmojiSelected;
            clickDetectionService.Resume();

            UIAudioEventsBus.Instance.SendPlayAudioEvent(emojiContainer.openEmojiPanelAudio);
        }

        protected override void Deactivate()
        {
            emojiPanelPresenter.SetPanelVisibility(false);
            emojiContainer.emojiPanelButton.SetState(false);
            emojiContainer.emojiPanel.EmojiContainer.gameObject.SetActive(false);
            emojiPanelPresenter.EmojiSelected -= OnEmojiSelected;
            clickDetectionService.Pause();
        }

        private void OnEmojiSelected(string emoji)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(emojiContainer.addEmojiAudio);
            if (!inputField.IsWithinCharacterLimit(emoji.Length)) return;
            inputField.InsertTextAtCaretPosition(emoji);
        }

        public void Dispose()
        {
            emojiPanelPresenter.Dispose();
        }
    }
}
