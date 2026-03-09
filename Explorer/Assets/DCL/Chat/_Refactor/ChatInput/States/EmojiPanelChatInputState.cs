using DCL.Audio;
using DCL.Chat.ChatServices;
using DCL.Emoji;
using DCL.UI.CustomInputField;
using MVC;
using System;

namespace DCL.Chat.ChatInput
{
    public class EmojiPanelChatInputState : IndependentMVCState, IDisposable
    {
        private readonly EmojiPanelPresenter emojiPanelPresenter;
        private readonly ChatInputView.EmojiContainer emojiContainer;
        private readonly CustomInputField inputField;
        private readonly ChatClickDetectionHandler clickDetectionHandler;

        public EmojiPanelChatInputState(ChatInputView view, EmojiPanelPresenter emojiPanelPresenter)
        {
            emojiContainer = view.emojiContainer;
            this.emojiPanelPresenter = emojiPanelPresenter;

            inputField = view.inputField;

            clickDetectionHandler = new ChatClickDetectionHandler(emojiContainer.emojiPanel.transform);
            clickDetectionHandler.OnClickOutside += Deactivate;
            clickDetectionHandler.Pause();
        }

        protected override void Activate()
        {
            emojiContainer.emojiPanel.ResetToDefaultPosition();
            emojiPanelPresenter.SetPanelVisibility(true);
            emojiContainer.emojiPanelButton.SetState(true);
            emojiPanelPresenter.EmojiSelected += OnEmojiSelected;
            clickDetectionHandler.Resume();

            UIAudioEventsBus.Instance.SendPlayAudioEvent(emojiContainer.openEmojiPanelAudio);
        }

        protected override void Deactivate()
        {
            emojiPanelPresenter.SetPanelVisibility(false);
            emojiContainer.emojiPanelButton.SetState(false);
            emojiPanelPresenter.EmojiSelected -= OnEmojiSelected;
            clickDetectionHandler.Pause();
        }

        private void OnEmojiSelected(string emoji)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(emojiContainer.addEmojiAudio);
            if (!inputField.IsWithinCharacterLimit(emoji.Length)) return;
            inputField.InsertTextAtCaretPosition(emoji);
        }

        public void Dispose() { }
    }
}
