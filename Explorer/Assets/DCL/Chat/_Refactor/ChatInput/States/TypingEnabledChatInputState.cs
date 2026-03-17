#if !NO_LIVEKIT_MODE

using DCL.Audio;
using DCL.Chat.ChatCommands;
using DCL.Chat.EventBus;
using DCL.UI.CustomInputField;
using DCL.UI.SuggestionPanel;
using MVC;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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

        private readonly IChatEventBus chatEventBus;

        private PasteToastState? pasteToastState;
        private SuggestionPanelChatInputState? suggestionPanelState;
        private EmojiPanelChatInputState? emojiPanelState;
        private bool isLocked;
        private CustomInputField inputField = null!;
        private CancellationTokenSource? suggestionCloseCts;
        private CancellationTokenSource? searchSuggestionCts;

        public TypingEnabledChatInputState(IChatEventBus chatEventBus)
        {
            this.chatEventBus = chatEventBus;
        }

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
            LockInputField(true);
            context.ChatInputView.Show();
            context.ChatInputView.ApplyFocusStyle();
            context.ChatInputView.SetActiveTyping();

            chatEventBus.InsertTextInChatRequested += InsertText;
            chatEventBus.ClearAndInsertTextInChatRequested += ClearAndInsertText;

            ViewDependencies.ClipboardManager.OnPaste += PasteClipboardText;
            inputField.onSubmit.AddListener(HandleMessageSubmitted);
            inputField.Clicked += InputFieldOnClicked;
            inputField.onValueChanged.AddListener(OnInputChanged);
            inputField.PasteShortcutPerformed += OnPasteShortcut;
            eventsScope.Add(context.InputEventBus.Subscribe<InputSuggestionsEvents.SuggestionSelectedEvent>(ReplaceSuggestionInText));

            inputField.onDeselect.AddListener(OnInputDeselected);
            context.ChatInputView.emojiContainer.emojiPanelButton.Button.onClick.AddListener(ToggleEmojiPanel);
            context.ChatInputView.UpdateCharacterCount();
        }

        public override void End()
        {
            LockInputField(false);
            chatEventBus.InsertTextInChatRequested -= InsertText;
            chatEventBus.ClearAndInsertTextInChatRequested -= ClearAndInsertText;

            ViewDependencies.ClipboardManager.OnPaste -= PasteClipboardText;
            inputField.onSubmit.RemoveListener(HandleMessageSubmitted);
            inputField.Clicked -= InputFieldOnClicked;
            inputField.onValueChanged.RemoveListener(OnInputChanged);
            inputField.PasteShortcutPerformed -= OnPasteShortcut;
            context.ChatInputView.emojiContainer.emojiPanelButton.Button.onClick.RemoveListener(ToggleEmojiPanel);
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
            UIAudioEventsBus.Instance.SendPlayAudioEvent(context.ChatInputView.chatInputTextAudio);
            pasteToastState!.TryDeactivate();
            context.ChatInputView.UpdateCharacterCount();

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
                    bool matchFound = await suggestionPanelState!.TryFindMatchAsync(inputText, ct);

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
            if (suggestionPanelState!.IsActive)
                return;

            emojiPanelState!.TryDeactivate();

            // NOTE: We need to select the input field again because
            // NOTE: the input field loses focus when the message is submitted
            inputField.SelectInputField();

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
                context.ChatInputView.SelectInputField();
            }
        }

        private async UniTaskVoid DeactivateSuggestionsNextFrameAsync(CancellationToken ct)
        {
            await UniTask.NextFrame(ct);

            if (!ct.IsCancellationRequested)
                suggestionPanelState!.TryDeactivate();
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
            context.ChatInputView.InsertTextAtCaretPosition(pastedText);
            pasteToastState!.TryDeactivate();
        }

        private void InsertText(string text)
        {
            context.ChatInputView.InsertTextAtCaretPosition(text);
        }

        private void ClearAndInsertText(string text)
        {
            context.ChatInputView.ClearAndInsertText(text);
        }

        protected override void OnInputBlocked()
        {
            ChangeState<BlockedChatInputState>();
        }

        protected override void OnInputUnblocked()
        {
            // Regain the focus on the input field
            context.ChatInputView.SelectInputField();
        }

        private void LockInputField(bool locked)
        {
            isLocked = locked;
        }

        private void OnInputDeselected(string text)
        {
            if (isLocked)
                context.ChatInputView.SelectInputField();
        }
    }
}

#endif
