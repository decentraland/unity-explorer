using DCL.Audio;
using DCL.Chat.ChatServices;
using DCL.Emoji;
using DCL.UI.CustomInputField;
using MVC;
using System;
using UnityEngine;

namespace DCL.Chat.ChatInput
{
    public class EmojiPanelChatInputState : IndependentMVCState, IDisposable
    {
        private readonly EmojiPanelPresenter emojiPanelPresenter;
        private readonly EmojiPanelView emojiPanelView;
        private readonly ChatInputView.EmojiContainer emojiContainer;
        private readonly CustomInputField inputField;
        private readonly RectTransform inputFieldRect;
        private readonly ChatClickDetectionHandler clickDetectionHandler;

        public EmojiPanelChatInputState(ChatInputView view, EmojiPanelPresenter emojiPanelPresenter, EmojiPanelView emojiPanelView)
        {
            emojiContainer = view.emojiContainer;
            this.emojiPanelPresenter = emojiPanelPresenter;
            this.emojiPanelView = emojiPanelView;

            inputField = view.inputField;

            // The field, not its container: the container keeps a fixed height while the field grows upwards as
            // the text wraps.
            inputFieldRect = (RectTransform)view.inputField.transform;

            clickDetectionHandler = new ChatClickDetectionHandler(
                emojiPanelView.transform,
                emojiContainer.emojiPanelButton.transform);
            clickDetectionHandler.OnClickOutside += HandleClickOutside;
            clickDetectionHandler.Pause();
        }

        protected override void Activate()
        {
            // Measured from the input field on every open: the chat panel is re-laid-out whenever the voice-chat
            // panel changes height, and the field itself grows with wrapped text — neither of which a stored
            // position would survive.
            emojiPanelView.PositionAbove(inputFieldRect, emojiContainer.emojiPanelGap);
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

        public void Dispose()
        {
            clickDetectionHandler.Dispose();
        }

        private void HandleClickOutside() => TryDeactivate();
    }
}
