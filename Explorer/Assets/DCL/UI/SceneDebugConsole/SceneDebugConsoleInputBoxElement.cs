using MVC;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DCL.UI.SceneDebugConsole
{
    //NOTE: This class is extracted from existing functionality in the ChatController and ChatView, so not all code is new, nor all code was refactored
    /// <summary>
    ///     This element condenses all the functionality related to the input box of the chat, including triggering suggestions, opening the emoji panel and updating the character counter
    /// </summary>
    public class SceneDebugConsoleInputBoxElement : MonoBehaviour, IViewWithGlobalDependencies
    {
        public delegate void InputBoxSelectionChangedDelegate(bool isSelected);
        public delegate void InputChangedDelegate(string input);
        public delegate void InputSubmittedDelegate(string message, string origin);

        private const string ORIGIN = "sceneDebugConsole";

        [SerializeField] private CustomInputField.CustomInputField inputField;
        [SerializeField] private GameObject outlineObject;
        [SerializeField] private LayoutElement layoutElement;

        private ViewDependencies viewDependencies;
        private bool isInputSelected;
        private bool isInputSubmissionEnabled;

        public string InputBoxText
        {
            get => inputField.text;
            set => inputField.text = value;
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        /// <summary>
        ///     Raised when either the input box is selected or deselected.
        /// </summary>
        public event InputBoxSelectionChangedDelegate? InputBoxSelectionChanged;

        /// <summary>
        ///     Raised whenever the user attempts to send the content of the input box as a chat message.
        /// </summary>
        public event InputSubmittedDelegate? InputSubmitted;

        /// <summary>
        ///     Raised whenever the input changes
        /// </summary>
        public event InputChangedDelegate? InputChanged;

        public void Initialize()
        {
            // device = InputSystem.GetDevice<Mouse>();
            inputField.onSelect.AddListener(OnInputSelected);
            inputField.onDeselect.AddListener(OnInputDeselected);
            inputField.onValueChanged.AddListener(OnInputChanged);
            inputField.PasteShortcutPerformed += OnPasteShortcutPerformed;
        }

        /// <summary>
        ///     Makes the input box stop receiving user inputs.
        /// </summary>
        public void DisableInputBoxSubmissions()
        {
            if(!isInputSubmissionEnabled) return;
            isInputSubmissionEnabled = false;

            // viewDependencies.ClipboardManager.OnPaste -= PasteClipboardText;
            viewDependencies.DclInput.UI.Close.performed -= OnUICloseInput;
            inputField.onSubmit.RemoveListener(OnInputFieldSubmitted);
            inputField.DeactivateInputField();
        }

        public void EnableInputBoxSubmissions()
        {
            if(isInputSubmissionEnabled) return;
            isInputSubmissionEnabled = true;

            inputField.onSubmit.AddListener(OnInputFieldSubmitted);
            // viewDependencies.ClipboardManager.OnPaste += PasteClipboardText;
            viewDependencies.DclInput.UI.Close.performed += OnUICloseInput;
        }

        public void FocusInputBox()
        {
            if (inputField.isFocused) return;

            inputField.SelectInputField();
        }

        private void OnPasteShortcutPerformed()
        {
            // viewDependencies.ClipboardManager.Paste(this);
        }

        private void OnInputChanged(string inputText)
        {
            //With this we are detecting only the last word (where the current caret position is) and checking for matches there.
            //This regex already pre-matches the starting patterns for both Emoji ":" and Profile "@" patterns, and only sends the match further to validate other specific conditions
            //This is needed because otherwise we wouldn't know which word in the whole text we are trying to match, and if there were several potential matches
            //it would always capture the first one instead of the current one.
            /*Match wordMatch = PRE_MATCH_PATTERN_REGEX.Match(inputText, 0, inputField.stringPosition);
            if (wordMatch.Success)
            {
                wordMatchIndex = wordMatch.Index;
                lastMatch = suggestionPanelController!.HandleSuggestionsSearch(wordMatch.Value, EMOJI_PATTERN_REGEX, InputSuggestionType.EMOJIS, emojiSuggestionsDictionary);

                //If we don't find any emoji pattern only then we look for username patterns
                if (!lastMatch.Success)
                {
                    UpdateProfileNameMap();
                    lastMatch = suggestionPanelController.HandleSuggestionsSearch(wordMatch.Value, PROFILE_PATTERN_REGEX, InputSuggestionType.PROFILE, profileSuggestionsDictionary);
                }
            }
            else
            {
                suggestionPanelController!.SetPanelVisibility(false);
                lastMatch = Match.Empty;
            }

            inputField.UpAndDownArrowsEnabled = !lastMatch.Success;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(chatInputTextAudio);
            closePopupTask.TrySetResult();
            characterCounter.SetCharacterCount(inputText.Length);
            layoutElement.preferredHeight = inputField.preferredHeight;*/
            InputChanged?.Invoke(inputText);
        }

        /// <summary>
        ///     Makes the input box gain the focus and replaces its content.
        /// </summary>
        /// <param name="text">The new content of the input box.</param>
        public void FocusInputBoxWithText(string text)
        {
            inputField.SelectInputField(text);
        }

        /// <summary>
        ///     Makes the chat submit the current content of the input box.
        /// </summary>
        public void SubmitInput()
        {
            inputField.OnSubmit(null);
        }

        /// <summary>
        ///     Performs a click event on the chat window.
        /// </summary>
        public void Click()
        {
        }

        private void PasteClipboardText(object sender, string pastedText)
        {
            InsertTextAtCaretPosition(pastedText);
        }

        public void InsertTextAtCaretPosition(string text)
        {
            inputField.InsertTextAtCaretPosition(text);
        }

        private void OnInputDeselected(string _)
        {
            outlineObject.SetActive(false);
            isInputSelected = false;
            InputBoxSelectionChanged?.Invoke(false);
        }

        private void OnInputSelected(string _)
        {
            InputBoxSelectionChanged?.Invoke(true);

            outlineObject.SetActive(true);

            if (isInputSelected) return;

            isInputSelected = true;
        }

        private void OnUICloseInput(InputAction.CallbackContext callbackContext)
        {
            inputField.DeactivateInputField();
            inputField.OnDeselect(null);
        }

        private void OnInputFieldSubmitted(string submittedText)
        {
            if (string.IsNullOrWhiteSpace(submittedText))
            {
                inputField.DeactivateInputField();
                inputField.OnDeselect(null);
                return;
            }

            inputField.ResetInputField();

            InputSubmitted?.Invoke(submittedText, ORIGIN);
        }

        public void Dispose()
        {
            inputField.PasteShortcutPerformed -= OnPasteShortcutPerformed;
        }
    }
}
