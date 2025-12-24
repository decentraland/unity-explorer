using DCL.Audio;
using DCL.Chat.ChatCommands;
using DCL.Chat.EventBus;
using DCL.UI.CustomInputField;
using DCL.UI.SuggestionPanel;
using MVC;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Emoji;
using DCL.UI.Profiles.Helpers;
using UnityEngine.EventSystems;
using Utility;

namespace DCL.Chat.ChatInput
{
    /// <summary>
    ///     Can type in the chat
    /// </summary>
    public class TypingEnabledChatInputState : ChatInputState, IDisposable
    {
        private readonly EventSubscriptionScope eventsScope = new ();

        private readonly ChatInputView view;
        private readonly IChatEventBus chatEventBus;
        private readonly GetParticipantProfilesCommand getParticipantProfilesCommand;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly SendMessageCommand sendMessageCommand;
        private readonly EmojiMapping emojiMapping;

        private PasteToastState? pasteToastState;
        private SuggestionPanelChatInputState? suggestionPanelState;
        private EmojiPanelChatInputState? emojiPanelState;
        private bool isLocked;
        private CustomInputField inputField = null!;
        private CancellationTokenSource? suggestionCloseCts;

        public TypingEnabledChatInputState(ChatInputView view, IChatEventBus chatEventBus,
            SendMessageCommand sendMessageCommand,
            EmojiMapping emojiMapping,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            GetParticipantProfilesCommand getParticipantProfilesCommand)
        {
            this.view = view;
            this.chatEventBus = chatEventBus;
            this.sendMessageCommand = sendMessageCommand;
            this.emojiMapping = emojiMapping;
            this.getParticipantProfilesCommand = getParticipantProfilesCommand;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
        }

        public void Dispose()
        {
            suggestionPanelState?.Dispose();
        }

        public override void OnInitialized()
        {
            pasteToastState = new PasteToastState(view, context, disposalCt);
            suggestionPanelState = new SuggestionPanelChatInputState(view, emojiMapping, profileRepositoryWrapper, getParticipantProfilesCommand, context);
            emojiPanelState = new EmojiPanelChatInputState(view, emojiMapping, context);
            inputField = view.inputField;
        }

        public override void Enter()
        {
            LockInputField(true);
            view.Show();
            view.ApplyFocusStyle();
            view.SetActiveTyping();

            chatEventBus.InsertTextInChatRequested += InsertText;
            chatEventBus.ClearAndInsertTextInChatRequested += ClearAndInsertText;

            ViewDependencies.ClipboardManager.OnPaste += PasteClipboardText;
            inputField.onSubmit.AddListener(HandleMessageSubmitted);
            inputField.Clicked += InputFieldOnClicked;
            inputField.onValueChanged.AddListener(OnInputChanged);
            inputField.PasteShortcutPerformed += OnPasteShortcut;
            eventsScope.Add(view.inputEventBus.Subscribe<InputSuggestionsEvents.SuggestionSelectedEvent>(ReplaceSuggestionInText));

            inputField.onDeselect.AddListener(OnInputDeselected);
            view.emojiContainer.emojiPanelButton.Button.onClick.AddListener(ToggleEmojiPanel);
            view.UpdateCharacterCount();
        }

        public override void Exit()
        {
            LockInputField(false);
            chatEventBus.InsertTextInChatRequested -= InsertText;
            chatEventBus.ClearAndInsertTextInChatRequested -= ClearAndInsertText;

            ViewDependencies.ClipboardManager.OnPaste -= PasteClipboardText;
            inputField.onSubmit.RemoveListener(HandleMessageSubmitted);
            inputField.Clicked -= InputFieldOnClicked;
            inputField.onValueChanged.RemoveListener(OnInputChanged);
            inputField.PasteShortcutPerformed -= OnPasteShortcut;
            view.emojiContainer.emojiPanelButton.Button.onClick.RemoveListener(ToggleEmojiPanel);
            inputField.onDeselect.RemoveListener(OnInputDeselected);
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

            LockInputField(!emojiPanelState.IsActive);
        }

        private void OnInputChanged(string inputText)
        {
            bool matchFound = suggestionPanelState!.TryFindMatch(inputText);

            UIAudioEventsBus.Instance.SendPlayAudioEvent(view.chatInputTextAudio);
            pasteToastState!.TryDeactivate();
            view.UpdateCharacterCount();

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

            // NOTE: We need to select the input field again because
            // NOTE: the input field loses focus when the message is submitted
            inputField.SelectInputField();

            if (string.IsNullOrWhiteSpace(message))
                return;

            inputField.ResetInputField();

            sendMessageCommand.Execute(new SendMessageCommandPayload { Body = message });
        }

        private void InputFieldOnClicked(PointerEventData.InputButton inputButton)
        {
            if (inputButton == PointerEventData.InputButton.Right && ViewDependencies.ClipboardManager.HasValue())
            {
                pasteToastState!.ReActivate();

                // TODO Input Field get deactivate when the focus is lost, we should find a better way to handle this
                view.SelectInputField();
            }
        }

        private async UniTaskVoid DeactivateSuggestionsNextFrameAsync(CancellationToken ct)
        {
            await UniTask.NextFrame(ct);

            if (!ct.IsCancellationRequested)
                suggestionPanelState.TryDeactivate();
        }

        private void ReplaceSuggestionInText(InputSuggestionsEvents.SuggestionSelectedEvent suggestion)
        {
            // Not great
            if (!suggestionPanelState!.IsActive)
                return;

            suggestionPanelState!.ReplaceSuggestionInText(suggestion.Id);
            // suggestionPanelState.TryDeactivate();

            suggestionCloseCts = suggestionCloseCts.SafeRestart();
            DeactivateSuggestionsNextFrameAsync(suggestionCloseCts.Token).Forget();
        }

        private void PasteClipboardText(object sender, string pastedText)
        {
            view.InsertTextAtCaretPosition(pastedText);
            pasteToastState!.TryDeactivate();
        }

        private void InsertText(string text)
        {
            view.InsertTextAtCaretPosition(text);
        }

        private void ClearAndInsertText(string text)
        {
            view.ClearAndInsertText(text);
        }

        protected override void OnInputBlocked()
        {
            machine.Enter<BlockedChatInputState>();
        }

        protected override void OnInputUnblocked()
        {
            // Regain the focus on the input field
            view.SelectInputField();
        }

        private void LockInputField(bool locked)
        {
            isLocked = locked;
        }

        private void OnInputDeselected(string text)
        {
            if (isLocked)
                view.SelectInputField();
        }
    }
}
