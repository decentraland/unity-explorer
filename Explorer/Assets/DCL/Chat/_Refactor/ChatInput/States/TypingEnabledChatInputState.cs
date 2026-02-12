using DCL.Audio;
using DCL.Chat.ChatCommands;
using DCL.UI.CustomInputField;
using DCL.UI.SuggestionPanel;
using MVC;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Emoji;
using DCL.UI.Profiles.Helpers;
using UnityEngine.EventSystems;
using Utility;

namespace DCL.Chat.ChatInput
{
    /// <summary>
    ///     Can type in the chat
    /// </summary>
    public class TypingEnabledChatInputState : ChatInputState, IState, IDisposable
    {
        private readonly EventSubscriptionScope eventsScope = new ();

        private readonly MVCStateMachine<ChatInputState> stateMachine;
        private readonly ChatInputView view;
        private readonly ChatEventBus chatEventBus;
        private readonly SendMessageCommand sendMessageCommand;

        private readonly PasteToastState pasteToastState;
        private readonly SuggestionPanelChatInputState suggestionPanelState;
        private readonly EmojiPanelChatInputState emojiPanelState;
        private readonly CustomInputField inputField;

        private CancellationTokenSource? suggestionCloseCts;
        private CancellationTokenSource? searchSuggestionCts;

        private bool isLocked;

        public TypingEnabledChatInputState(
            MVCStateMachine<ChatInputState> stateMachine,
            ChatInputView view,
            ChatEventBus chatEventBus,
            SendMessageCommand sendMessageCommand,
            EmojiMapping emojiMapping,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            GetParticipantProfilesCommand getParticipantProfilesCommand,
            CancellationToken stateMachineDisposalCt)
        {
            this.stateMachine = stateMachine;
            this.view = view;
            this.chatEventBus = chatEventBus;
            this.sendMessageCommand = sendMessageCommand;

            pasteToastState = new PasteToastState(view, stateMachineDisposalCt);
            suggestionPanelState = new SuggestionPanelChatInputState(view, emojiMapping, profileRepositoryWrapper, getParticipantProfilesCommand);
            emojiPanelState = new EmojiPanelChatInputState(view, emojiMapping);
            inputField = view.inputField;
        }

        public void Dispose()
        {
            suggestionPanelState.Dispose();
        }

        public void Enter()
        {
            LockInputField(true);
            view.Show();
            view.ApplyFocusStyle();
            view.SetActiveTyping();

            eventsScope.Add(chatEventBus.Subscribe<ChatEvents.InsertTextInChatRequestedEvent>(evt => InsertText(evt.Text)));
            eventsScope.Add(chatEventBus.Subscribe<ChatEvents.ClearAndInsertTextInChatRequestedEvent>(evt => ClearAndInsertText(evt.Text)));

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
            eventsScope.Dispose();

            ViewDependencies.ClipboardManager.OnPaste -= PasteClipboardText;
            inputField.onSubmit.RemoveListener(HandleMessageSubmitted);
            inputField.Clicked -= InputFieldOnClicked;
            inputField.onValueChanged.RemoveListener(OnInputChanged);
            inputField.PasteShortcutPerformed -= OnPasteShortcut;
            view.emojiContainer.emojiPanelButton.Button.onClick.RemoveListener(ToggleEmojiPanel);
            inputField.onDeselect.RemoveListener(OnInputDeselected);
            eventsScope.Dispose();

            pasteToastState.TryDeactivate();
            suggestionPanelState.TryDeactivate();
            emojiPanelState.TryDeactivate();
        }

        private void ToggleEmojiPanel()
        {
            if (!emojiPanelState.IsActive)
            {
                emojiPanelState.TryActivate();
                suggestionPanelState.TryDeactivate();
            }
            else { emojiPanelState.TryDeactivate(); }

            LockInputField(!emojiPanelState.IsActive);
        }

        private void OnInputChanged(string inputText)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(view.chatInputTextAudio);
            pasteToastState.TryDeactivate();
            view.UpdateCharacterCount();

            searchSuggestionCts = searchSuggestionCts.SafeRestart();
            SuggestElementsAndShowPanelAsync(searchSuggestionCts.Token).Forget();
            return;

            async UniTaskVoid SuggestElementsAndShowPanelAsync(CancellationToken ct)
            {
                try
                {
                    // Fixes https://github.com/decentraland/unity-explorer/issues/6965
                    // This operation needs to be awaited otherwise a race condition occurs
                    // between the suggested elements generated and the submitted element processed once the panel is activated
                    bool matchFound = await suggestionPanelState.TryFindMatchAsync(inputText, ct);

                    if (matchFound)
                        suggestionPanelState.TryActivate();
                    else
                        suggestionPanelState.TryDeactivate();
                }
                catch (OperationCanceledException) { }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES); }
            }
        }

        private void OnPasteShortcut()
        {
            ViewDependencies.ClipboardManager.Paste(this);
        }

        private void HandleMessageSubmitted(string message)
        {
            // Not great
            if (suggestionPanelState.IsActive)
                return;

            emojiPanelState.TryDeactivate();

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
                pasteToastState.ReActivate();

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
            if (!suggestionPanelState.IsActive)
                return;

            suggestionPanelState.ReplaceSuggestionInText(suggestion.Id);

            // suggestionPanelState.TryDeactivate();

            suggestionCloseCts = suggestionCloseCts.SafeRestart();
            DeactivateSuggestionsNextFrameAsync(suggestionCloseCts.Token).Forget();
        }

        private void PasteClipboardText(object sender, string pastedText)
        {
            view.InsertTextAtCaretPosition(pastedText);
            pasteToastState.TryDeactivate();
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
            stateMachine.Enter<BlockedChatInputState>();
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
