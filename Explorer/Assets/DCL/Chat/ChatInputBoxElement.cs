using Castle.Core.Internal;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Emoji;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.CustomInputField;
using DCL.UI.SuggestionPanel;
using MVC;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Chat
{
    public class ChatInputBoxElement : MonoBehaviour, IViewWithGlobalDependencies
    {
        public delegate void EmojiSelectionVisibilityChangedDelegate(bool isVisible);
        public delegate void InputBoxSelectionChangedDelegate(bool isSelected);
        public delegate void InputChangedDelegate(string input);
        public delegate void InputSubmittedDelegate(string message, string origin);

        private const string ORIGIN = "chat";
        private static readonly Regex EMOJI_PATTERN_REGEX = new (@"(?<!https?:)(:\w{2,10})", RegexOptions.Compiled);
        private static readonly Regex PROFILE_PATTERN_REGEX = new (@"(?:^|\s)@([A-Za-z0-9]{1,15})(?=\s|$)", RegexOptions.Compiled);

        [SerializeField] private CustomInputField inputField;
        [SerializeField] private CharacterCounterView characterCounter;
        [SerializeField] private RectTransform pastePopupPosition;

        [Header("Emojis")]
        [SerializeField] private EmojiPanelConfigurationSO emojiPanelConfiguration;
        [SerializeField] private EmojiButton emojiButtonPrefab;
        [SerializeField] private TextAsset emojiMappingJson;
        [SerializeField] private EmojiSectionView emojiSectionViewPrefab;
        [SerializeField] private EmojiButtonView emojiPanelButton;
        [SerializeField] private EmojiPanelView emojiPanel;

        [Header("Suggestion Panel")]
        [SerializeField] private InputSuggestionPanelElement suggestionPanel;

        [Header("Audio")]
        [SerializeField] private AudioClipConfig addEmojiAudio;
        [SerializeField] private AudioClipConfig openEmojiPanelAudio;
        [SerializeField] private AudioClipConfig chatSendMessageAudio;
        [SerializeField] private AudioClipConfig chatInputTextAudio;
        [SerializeField] private AudioClipConfig enterInputAudio;

        private readonly Dictionary<InputSuggestionType, Dictionary<string, IInputSuggestionElementData>> suggestionsPerTypeMap = new ();

        private UniTaskCompletionSource closePopupTask;
        private Mouse device;
        private EmojiPanelController? emojiPanelController;
        private InputSuggestionPanelController? suggestionPanelController;

        private CancellationTokenSource emojiPanelCts = new ();
        private bool isInputSelected;
        private string? lastMatch;

        private ViewDependencies viewDependencies;

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
        ///     Raised when either the emoji selection panel opens or closes.
        /// </summary>
        public event EmojiSelectionVisibilityChangedDelegate? EmojiSelectionVisibilityChanged;

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
            device = InputSystem.GetDevice<Mouse>();

            InitializeEmojiPanelController();
            InitializeEmojiMapping(emojiPanelController!.EmojiNameMapping);
            InitializeProfilesMapping();

            suggestionPanelController = new InputSuggestionPanelController(suggestionPanel, viewDependencies);
            suggestionPanelController.SuggestionSelectedEvent += OnSuggestionSelected;

            inputField.onSelect.AddListener(OnInputSelected);
            inputField.onDeselect.AddListener(OnInputDeselected);
            inputField.onValueChanged.AddListener(OnInputChanged);
            inputField.onSubmit.AddListener(InputFieldSubmitEvent);
            inputField.OnRightClickEvent += OnRightClickRegistered;
            inputField.OnPasteShortcutDetectedEvent += OnPasteShortcutDetected;

            characterCounter.SetMaximumLength(inputField.characterLimit);
            characterCounter.gameObject.SetActive(false);


            closePopupTask = new UniTaskCompletionSource();
        }

        /// <summary>
        ///     Makes the input box stop receiving user inputs.
        /// </summary>
        public void DisableInputBoxSubmissions()
        {
            viewDependencies.ClipboardManager.OnPaste -= PasteClipboardText;
            inputField.onSubmit.RemoveListener(InputFieldSubmitEvent);
            inputField.DeactivateInputField();
        }

        public void EnableInputBoxSubmissions()
        {
            inputField.onSubmit.AddListener(InputFieldSubmitEvent);
        }

        public void ClosePopups()
        {
            closePopupTask.TrySetResult();
        }

        public void FocusInputBox()
        {
            if (suggestionPanel.IsActive) return;

            if (inputField.isFocused) return;

            inputField.SelectInputField();
        }

        private void OnPasteShortcutDetected()
        {
            viewDependencies.ClipboardManager.Paste(this);
        }

        private void OnInputChanged(string inputText)
        {
            lastMatch = suggestionPanelController!.HandleSuggestionsSearch(inputText, EMOJI_PATTERN_REGEX, InputSuggestionType.EMOJIS, suggestionsPerTypeMap[InputSuggestionType.EMOJIS]);

            //If we don't find any emoji pattern only then we look for username patterns
            //TODO FRAN: MAKE SURE WE ONLY DETECT THE LATEST USERNAME PATTERN, IF THERE IS ONE ALREADY DETECTED WE SHOULD NOT DETECT IT AGAIN
            if (lastMatch.IsNullOrEmpty())
            {
                UpdateProfileNameMap();
                lastMatch = suggestionPanelController.HandleSuggestionsSearch(inputText, PROFILE_PATTERN_REGEX, InputSuggestionType.PROFILE, suggestionsPerTypeMap[InputSuggestionType.PROFILE]);
            }

            inputField.UpAndDownArrowsEnabled = lastMatch.IsNullOrEmpty();

            UIAudioEventsBus.Instance.SendPlayAudioEvent(chatInputTextAudio);
            closePopupTask.TrySetResult();
            characterCounter.SetCharacterCount(inputText.Length);
            InputChanged?.Invoke(inputText);
        }

        /// <summary>
        ///     Makes the input box gain the focus and replaces its content.
        /// </summary>
        /// <param name="text">The new content of the input box.</param>
        public void FocusInputBoxWithText(string text)
        {
            if (inputField.isFocused)
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
            CheckIfClickedOnEmojiPanel(); //TODO FRAN: This should work with callbacks from the panels, not by checking raycasts??

            void CheckIfClickedOnEmojiPanel()
            {
                if (!(emojiPanel.gameObject.activeInHierarchy ||
                      suggestionPanel.gameObject.activeInHierarchy)) return;

                IReadOnlyList<RaycastResult> raycastResults = viewDependencies.EventSystem.RaycastAll(device.position.value);
                var clickedOnPanel = false;

                foreach (RaycastResult result in raycastResults)
                    if (result.gameObject == emojiPanel.gameObject ||
                        result.gameObject == emojiPanelButton.gameObject ||
                        result.gameObject == suggestionPanel.ScrollViewRect.gameObject)
                        clickedOnPanel = true;

                if (!clickedOnPanel)
                {
                    if (emojiPanel.gameObject.activeInHierarchy)
                    {
                        emojiPanelButton.SetState(false);
                        emojiPanel.gameObject.SetActive(false);
                        EmojiSelectionVisibilityChanged?.Invoke(false);
                    }

                    suggestionPanelController.SetPanelVisibility(false);
                }
            }
        }

        private void OnRightClickRegistered()
        {
            if (isInputSelected && viewDependencies.ClipboardManager.HasValue())
            {
                viewDependencies.ClipboardManager.OnPaste -= PasteClipboardText;
                viewDependencies.ClipboardManager.OnPaste += PasteClipboardText;
                closePopupTask.TrySetResult();
                closePopupTask = new UniTaskCompletionSource();

                var data = new PastePopupToastData(
                    pastePopupPosition.position,
                    closePopupTask.Task);

                viewDependencies.GlobalUIViews.ShowPastePopupToastAsync(data);
                inputField.ActivateInputField();
                InputChanged?.Invoke(inputField.text);
            }
        }

        private void ToggleEmojiPanel()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(openEmojiPanelAudio);

            emojiPanelCts = emojiPanelCts.SafeRestart();
            bool toggle = !emojiPanel.gameObject.activeInHierarchy;
            emojiPanel.gameObject.SetActive(toggle);
            emojiPanelButton.SetState(toggle);
            suggestionPanelController!.SetPanelVisibility(false);
            emojiPanel.EmojiContainer.gameObject.SetActive(toggle);
            inputField.ActivateInputField();

            EmojiSelectionVisibilityChanged?.Invoke(toggle);
        }

        private void PasteClipboardText(object sender, string pastedText)
        {
            inputField.InsertTextAtSelectedPosition(pastedText);
            characterCounter.SetCharacterCount(inputField.text.Length);
        }


        private void OnInputDeselected(string _)
        {
            isInputSelected = false;
            emojiPanelButton.SetColor(false);
            characterCounter.gameObject.SetActive(false);
            InputBoxSelectionChanged?.Invoke(false);
        }

        private void OnInputSelected(string _)
        {
            InputBoxSelectionChanged?.Invoke(true);

            UIAudioEventsBus.Instance.SendPlayAudioEvent(enterInputAudio);

            if (isInputSelected) return;

            isInputSelected = true;
            emojiPanelButton.SetColor(true);
            characterCounter.gameObject.SetActive(true);
        }

        private void InputFieldSubmitEvent(string submittedText)
        {
            if (suggestionPanel.IsActive)
            {
                suggestionPanelController!.SetPanelVisibility(false);
                lastMatch = null;
                return;
            }

            if (emojiPanel.gameObject.activeInHierarchy)
            {
                emojiPanelButton.SetState(false);
                emojiPanelController!.SetPanelVisibility(false);
                EmojiSelectionVisibilityChanged?.Invoke(false);
            }

            if (string.IsNullOrWhiteSpace(submittedText))
            {
                inputField.DeactivateInputField();
                inputField.OnDeselect(null);
                return;
            }

            //Send message and clear Input Field
            UIAudioEventsBus.Instance.SendPlayAudioEvent(chatSendMessageAudio);

            inputField.ResetInputField();
            submittedText = viewDependencies.HyperlinkTextFormatter.FormatText(submittedText);

            InputSubmitted?.Invoke(submittedText, ORIGIN);
        }

        public void Dispose()
        {
            if (emojiPanelController != null)
            {
                emojiPanelController.EmojiSelected -= AddEmojiToInput;
                emojiPanelController.Dispose();
            }

            if (suggestionPanelController != null)
            {
                suggestionPanelController.Dispose();
                suggestionPanelController.SuggestionSelectedEvent -= OnSuggestionSelected;
            }

            emojiPanelCts.SafeCancelAndDispose();

            inputField.OnRightClickEvent -= OnRightClickRegistered;
            inputField.OnPasteShortcutDetectedEvent -= OnPasteShortcutDetected;
        }

        private void OnSuggestionSelected(string suggestionId)
        {
            ReplaceSuggestionInText(suggestionId);
        }

        private void ReplaceSuggestionInText(string suggestion)
        {
            if (lastMatch == null || !inputField.IsWithinCharacterLimit(suggestion.Length)) return;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(addEmojiAudio);

            inputField.ReplaceText(lastMatch, suggestion);

            inputField.ActivateInputField();
        }

        private void UpdateProfileNameMap()
        {
            //NOTE: This information should come from the channel where this chat is taking place and that channel should make sure this list is updated.
            //For now this will work, but is not the final implementation.
            IReadOnlyCollection<string> remoteParticipantIdentities = viewDependencies.RoomHub.IslandRoom().Participants.RemoteParticipantIdentities();

            //We Remove participants that are no longer in the island
            foreach (string key in suggestionsPerTypeMap[InputSuggestionType.PROFILE].Keys)
            {
                IInputSuggestionElementData? suggestionData = suggestionsPerTypeMap[InputSuggestionType.PROFILE][key];

                if (!remoteParticipantIdentities.Contains(suggestionData.GetId()))
                    suggestionsPerTypeMap[InputSuggestionType.PROFILE].Remove(key);
            }

            //We add or update the remaining participants
            foreach (string? participant in remoteParticipantIdentities)
            {
                Profile? profile = viewDependencies.ProfileCache.Get(participant);

                if (profile != null)
                {
                    if (suggestionsPerTypeMap[InputSuggestionType.PROFILE].TryGetValue(profile.DisplayName, out IInputSuggestionElementData? suggestionData) && suggestionData is ProfileInputSuggestionData profileSuggestionData)
                    {
                        if (profileSuggestionData.ProfileData != profile)
                            suggestionsPerTypeMap[InputSuggestionType.PROFILE][profile.DisplayName] = new ProfileInputSuggestionData(profile, profileSuggestionData.UsernameColor);
                    }
                    else
                    {
                        //Color should be stored in the profile so we dont re-calculate it for every place we use it. Leave this for future implementation along with profile picture.
                        Color color = viewDependencies.ProfileNameColorHelper.GetNameColor(profile.DisplayName);
                        suggestionsPerTypeMap[InputSuggestionType.PROFILE].TryAdd(profile.DisplayName, new ProfileInputSuggestionData(profile, color));
                    }
                }
            }
        }

#region Emojis
        private void InitializeEmojiPanelController()
        {
            emojiPanelController = new EmojiPanelController(emojiPanel, emojiPanelConfiguration, emojiMappingJson, emojiSectionViewPrefab, emojiButtonPrefab);
            emojiPanelController.EmojiSelected += AddEmojiToInput;
            emojiPanelButton.Button.onClick.AddListener(ToggleEmojiPanel);
        }

        private void InitializeEmojiMapping(Dictionary<string, EmojiData> emojiNameDataMapping)
        {
            suggestionsPerTypeMap.Add(InputSuggestionType.EMOJIS, new Dictionary<string, IInputSuggestionElementData>());

            foreach ((string emojiName, EmojiData emojiData) in emojiNameDataMapping)
                suggestionsPerTypeMap[InputSuggestionType.EMOJIS][emojiName] = new EmojiInputSuggestionData(emojiData.EmojiCode, emojiData.EmojiName);
        }

        private void InitializeProfilesMapping()
        {
            suggestionsPerTypeMap.Add(InputSuggestionType.PROFILE, new Dictionary<string, IInputSuggestionElementData>());
        }


        private void AddEmojiToInput(string emoji)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(addEmojiAudio);

            if (!inputField.IsWithinCharacterLimit(emoji.Length)) return;

            inputField.InsertTextAtSelectedPosition(emoji);
        }
#endregion
    }
}
