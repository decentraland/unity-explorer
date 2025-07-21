using DCL.Audio;
using DCL.Chat.ChatUseCases;
using DCL.UI.CustomInputField;
using DCL.UI.SuggestionPanel;
using MVC;
using System;
using UnityEngine.EventSystems;
using Utility;

namespace DCL.Chat
{
    /// <summary>
    ///     Can type in the chat
    /// </summary>
    public class TypingEnabledChatInputState : ChatInputState, IDisposable
    {
        private readonly EventSubscriptionScope eventsScope = new ();

        private PasteToastState? pasteToastState;
        private SuggestionPanelChatInputState? suggestionPanelState;
        private EmojiPanelChatInputState? emojiPanelState;

        private CustomInputField inputField = null!;

        public void Dispose()
        {
            suggestionPanelState?.Dispose();
        }

        public override void OnInitialized()
        {
            pasteToastState = new PasteToastState(context, disposalCt);
            suggestionPanelState = new SuggestionPanelChatInputState(context);
            emojiPanelState = new EmojiPanelChatInputState(context);
            inputField = context.ChatInputView.inputField;
        }

        public override void Begin()
        {
            context.ChatInputView.Show();
            context.ChatInputView.ApplyFocusStyle();
            context.ChatInputView.SetActiveTyping();

            ViewDependencies.ClipboardManager.OnPaste += PasteClipboardText;
            inputField.onSubmit.AddListener(HandleMessageSubmitted);
            inputField.Clicked += InputFieldOnClicked;
            inputField.onValueChanged.AddListener(OnInputChanged);
            inputField.PasteShortcutPerformed += OnPasteShortcut;
            eventsScope.Add(context.InputEventBus.Subscribe<InputSuggestionsEvents.SuggestionSelectedEvent>(ReplaceSuggestionInText));

            context.ChatInputView.emojiContainer.emojiPanelButton.Button.onClick.AddListener(ToggleEmojiPanel);
        }

        public override void End()
        {
            ViewDependencies.ClipboardManager.OnPaste -= PasteClipboardText;
            inputField.onSubmit.RemoveListener(HandleMessageSubmitted);
            inputField.Clicked -= InputFieldOnClicked;
            inputField.onValueChanged.RemoveListener(OnInputChanged);
            inputField.PasteShortcutPerformed -= OnPasteShortcut;
            context.ChatInputView.emojiContainer.emojiPanelButton.Button.onClick.RemoveListener(ToggleEmojiPanel);

            eventsScope.Dispose();

            pasteToastState!.TryDeactivate();
            suggestionPanelState!.TryDeactivate();
            emojiPanelState!.TryDeactivate();
        }

        private void ToggleEmojiPanel()
        {
            if (!emojiPanelState!.IsActive)
            {
                emojiPanelState.TryActivate();
                suggestionPanelState!.TryDeactivate();
            }
            else { emojiPanelState.TryDeactivate(); }
        }

        private void OnInputChanged(string inputText)
        {
            bool matchFound = suggestionPanelState!.TryFindMatch(inputText);

            UIAudioEventsBus.Instance.SendPlayAudioEvent(context.ChatInputView.chatInputTextAudio);
            pasteToastState!.TryDeactivate();
            context.ChatInputView.UpdateCharacterCount();

            if (matchFound)
                suggestionPanelState.TryActivate();
            else
                suggestionPanelState.TryDeactivate();
        }

        private void OnPasteShortcut()
        {
            ViewDependencies.ClipboardManager.Paste(this);
        }

        private void HandleMessageSubmitted(string message)
        {
            // Not great
            if (suggestionPanelState!.IsActive)
                return;

            emojiPanelState!.TryDeactivate();

            if (string.IsNullOrWhiteSpace(message))
                return;

            inputField.ResetInputField();

            context.SendMessageCommand.Execute(new SendMessageCommandPayload { Body = message });
        }

        private void InputFieldOnClicked(PointerEventData.InputButton inputButton)
        {
            if (inputButton == PointerEventData.InputButton.Right && ViewDependencies.ClipboardManager.HasValue())
            {
                pasteToastState!.ReActivate();

                // TODO Input Field get deactivate when the focus is lost, we should find a better way to handle this
                context.ChatInputView.SetActiveTyping();
            }
        }

        private void ReplaceSuggestionInText(InputSuggestionsEvents.SuggestionSelectedEvent suggestion)
        {
            // Not great
            if (!suggestionPanelState!.IsActive)
                return;

            suggestionPanelState!.ReplaceSuggestionInText(suggestion.Id);
            suggestionPanelState.TryDeactivate();
        }

        private void PasteClipboardText(object sender, string pastedText)
        {
            context.ChatInputView.InsertTextAtCaretPosition(pastedText);
            pasteToastState!.TryDeactivate();
        }
    }
}
