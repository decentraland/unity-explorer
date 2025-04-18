using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Emoji;
using DCL.Profiles;
using DCL.Settings.Settings;
using DCL.UI;
using DCL.UI.CustomInputField;
using DCL.UI.SuggestionPanel;
using MVC;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Pool;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat
{
    //NOTE: This class is extracted from existing functionality in the ChatController and ChatView, so not all code is new, nor all code was refactored
    /// <summary>
    ///     This element condenses all the functionality related to the input box of the chat, including triggering suggestions, opening the emoji panel and updating the character counter
    /// </summary>
    public class ChatInputBoxElement : MonoBehaviour, IViewWithGlobalDependencies
    {
        public delegate void EmojiSelectionVisibilityChangedDelegate(bool isVisible);
        public delegate void InputBoxFocusChangedDelegate(bool isFocused);
        public delegate void InputChangedDelegate(string input);
        public delegate void InputSubmittedDelegate(string messageToSend, string origin);

        private const string ORIGIN = "chat";
        private static readonly Regex EMOJI_PATTERN_REGEX = new (@"(?<!https?:)(:\w{2,10})", RegexOptions.Compiled);
        private static readonly Regex PROFILE_PATTERN_REGEX = new (@"(?:^|\s)@([A-Za-z0-9]{1,15})(?=\s|$)", RegexOptions.Compiled);
        private static readonly Regex PRE_MATCH_PATTERN_REGEX = new (@"(?<=^|\s)([@:]\S+)$", RegexOptions.Compiled);

        [SerializeField] private CustomInputField inputField;
        [SerializeField] private CharacterCounterView characterCounter;
        [SerializeField] private RectTransform pastePopupPosition;
        [SerializeField] private GameObject outlineObject;
        [SerializeField] private LayoutElement layoutElement;

        [Header("Emojis")]
        [SerializeField] private EmojiPanelConfigurationSO emojiPanelConfiguration;
        [SerializeField] private EmojiButton emojiButtonPrefab;
        [SerializeField] private TextAsset emojiMappingJson;
        [SerializeField] private EmojiSectionView emojiSectionViewPrefab;
        [SerializeField] private EmojiButtonView emojiPanelButton;
        [SerializeField] private EmojiPanelView emojiPanel;

        [Header("Suggestion Panel")]
        [SerializeField] private InputSuggestionPanelView suggestionPanel;

        [Header("Audio")]
        [SerializeField] private AudioClipConfig addEmojiAudio;
        [SerializeField] private AudioClipConfig openEmojiPanelAudio;
        [SerializeField] private AudioClipConfig chatSendMessageAudio;
        [SerializeField] private AudioClipConfig chatInputTextAudio;
        [SerializeField] private AudioClipConfig enterInputAudio;

        private readonly Dictionary<string, ProfileInputSuggestionData> profileSuggestionsDictionary = new ();
        private readonly Dictionary<string, EmojiInputSuggestionData> emojiSuggestionsDictionary = new ();

        private UniTaskCompletionSource closePopupTask;
        private Mouse device;
        private EmojiPanelController? emojiPanelController;
        private InputSuggestionPanelController? suggestionPanelController;
        private ViewDependencies viewDependencies;
        private IProfileCache profileCache;

        private CancellationTokenSource emojiPanelCts = new ();
        private bool isInputFocused;
        private Match lastMatch = Match.Empty;
        private int wordMatchIndex;
        private ChatSettingsAsset chatSettings;
        private CancellationTokenSource popupCts;

        private GetParticipantProfilesDelegate GetParticipantProfiles;
        private readonly List<Profile> participantProfiles = new ();

        private bool isInputSubmissionEnabled;

        public string InputBoxText
        {
            get => inputField.text;
            set => inputField.text = value;
        }

        /// <summary>
        /// Gets whether the paste context menu is visible.
        /// </summary>
        public bool IsPasteMenuOpen { get; private set; }

        /// <summary>
        /// Gets or sets whether the selected state of the input box should stay unchanged even if clicking outside.
        /// </summary>
        public bool LockSelectedState { get; set; }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        private bool IsEmojisEnabled
        {
            get => emojiPanelButton.gameObject.activeSelf;
            set => emojiPanelButton.gameObject.SetActive(value);
        }

        /// <summary>
        ///     Raised when either the input box is focused or unfocused.
        /// </summary>
        public event InputBoxFocusChangedDelegate? InputBoxFocusChanged;

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

        public void Initialize(ChatSettingsAsset chatSettings, GetParticipantProfilesDelegate getParticipantProfiles)
        {
            device = InputSystem.GetDevice<Mouse>();
            this.chatSettings = chatSettings;
            this.GetParticipantProfiles = getParticipantProfiles;

            InitializeEmojiPanelController();
            InitializeEmojiMapping(emojiPanelController!.EmojiNameMapping);

            suggestionPanelController = new InputSuggestionPanelController(suggestionPanel, viewDependencies);
            suggestionPanelController.SuggestionSelected += OnSuggestionSelected;

            inputField.onSelect.AddListener(OnInputSelected);
            inputField.onDeselect.AddListener(OnInputDeselected);
            inputField.onValueChanged.AddListener(OnInputChanged);
            inputField.Clicked += OnClicked;
            inputField.PasteShortcutPerformed += OnPasteShortcutPerformed;

            characterCounter.SetMaximumLength(inputField.characterLimit);
            characterCounter.gameObject.SetActive(false);

            IsEmojisEnabled = false;

            closePopupTask = new UniTaskCompletionSource();
        }

        /// <summary>
        ///     Makes the input box stop receiving user inputs.
        /// </summary>
        public void DisableInputBoxSubmissions()
        {
            if(!isInputSubmissionEnabled)
                return;
            isInputSubmissionEnabled = false;

            viewDependencies.ClipboardManager.OnPaste -= PasteClipboardText;
            viewDependencies.DclInput.UI.Close.performed -= OnUICloseInput;
            //inputField.onSubmit.RemoveListener(OnInputFieldSubmitted);
            inputField.DeactivateInputField();
        }

        public void EnableInputBoxSubmissions()
        {
            if(isInputSubmissionEnabled)
                return;
            isInputSubmissionEnabled = true;

            //inputField.onSubmit.AddListener(OnInputFieldSubmitted);
            viewDependencies.ClipboardManager.OnPaste += PasteClipboardText;
            viewDependencies.DclInput.UI.Close.performed += OnUICloseInput;
        }

        public void ClosePopups()
        {
            closePopupTask.TrySetResult();

            if(emojiPanel.gameObject.activeInHierarchy)
                IsEmojiPanelVisible = false;
        }

        public void Focus(string? newText = null)
        {
            if (suggestionPanel.IsActive) return;

            inputField.SelectInputField(newText);
        }

        public void Blur()
        {
            ClosePopups();
            inputField.onDeselect.Invoke(inputField.text);
        }

        private void OnPasteShortcutPerformed()
        {
            viewDependencies.ClipboardManager.Paste(this);
        }

        private void OnInputChanged(string inputText)
        {
            //With this we are detecting only the last word (where the current caret position is) and checking for matches there.
            //This regex already pre-matches the starting patterns for both Emoji ":" and Profile "@" patterns, and only sends the match further to validate other specific conditions
            //This is needed because otherwise we wouldn't know which word in the whole text we are trying to match, and if there were several potential matches
            //it would always capture the first one instead of the current one.
            Match wordMatch = PRE_MATCH_PATTERN_REGEX.Match(inputText, 0, inputField.stringPosition);
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
            layoutElement.preferredHeight = inputField.preferredHeight;
            InputChanged?.Invoke(inputText);
        }

        /// <summary>
        ///     Makes the chat submit the current content of the input box.
        /// </summary>
        public void SubmitInput()
        {
            inputField.OnSubmit(null);
        }

        /// <summary>
        ///     Called when a click event occurs in the parent object.
        /// </summary>
        public void OnClicked(IReadOnlyList<RaycastResult> raycastResults)
        {
            //TODO FRAN Issue #3317 after release: This could work with callbacks from the panels, not by checking raycasts.
            if (!(emojiPanel.gameObject.activeInHierarchy ||
                  suggestionPanel.gameObject.activeInHierarchy)) return;

            var clickedOnPanel = false;

            foreach (RaycastResult result in raycastResults)
                if (result.gameObject == emojiPanel.gameObject ||
                    result.gameObject == emojiPanelButton.gameObject ||
                    result.gameObject == suggestionPanel.ScrollViewRect.gameObject)
                    clickedOnPanel = true;

            if (!clickedOnPanel)
            {
                if (emojiPanel.gameObject.activeInHierarchy)
                    IsEmojiPanelVisible = false;

                suggestionPanelController.SetPanelVisibility(false);
            }
        }

        private void OnClicked(PointerEventData.InputButton button)
        {
            if (button == PointerEventData.InputButton.Right && isInputFocused && viewDependencies.ClipboardManager.HasValue())
            {
                IsPasteMenuOpen = true;

                closePopupTask.TrySetResult();
                closePopupTask = new UniTaskCompletionSource();

                var data = new PastePopupToastData(
                    pastePopupPosition.position,
                    closePopupTask.Task);

                popupCts = popupCts.SafeRestart();
                viewDependencies.GlobalUIViews.ShowPastePopupToastAsync(data, popupCts.Token).Forget();
                inputField.ActivateInputField();
                InputChanged?.Invoke(inputField.text);
            }

            Focus();
        }

        private bool IsEmojiPanelVisible
        {
            get => emojiPanel.gameObject.activeInHierarchy;

            set
            {
                if (emojiPanel.gameObject.activeInHierarchy != value)
                {
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(openEmojiPanelAudio);

                    emojiPanelCts = emojiPanelCts.SafeRestart();

                    emojiPanel.gameObject.SetActive(value);
                    emojiPanelButton.SetState(value);
                    suggestionPanelController!.SetPanelVisibility(false);
                    emojiPanel.EmojiContainer.gameObject.SetActive(value);

                    EmojiSelectionVisibilityChanged?.Invoke(value);
                }
            }
        }

        private void PasteClipboardText(object sender, string pastedText)
        {
            IsPasteMenuOpen = false;
            InsertTextAtCaretPosition(pastedText);
        }

        public void InsertTextAtCaretPosition(string text)
        {
            inputField.InsertTextAtCaretPosition(text);
            characterCounter.SetCharacterCount(inputField.text.Length);
        }

        private void OnInputDeselected(string _)
        {
            if (LockSelectedState)
            {
                inputField.ActivateInputField(); // So it does not hide the caret
                return;
            }

            outlineObject.SetActive(false);
            isInputFocused = false;
            emojiPanelButton.SetColor(false);
            IsEmojisEnabled = false;
            characterCounter.gameObject.SetActive(false);
            InputBoxFocusChanged?.Invoke(false);
        }

        private void OnInputSelected(string _)
        {
            if (isInputFocused) return;

            outlineObject.SetActive(true);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(enterInputAudio);

            isInputFocused = true;
            IsEmojisEnabled = true;
            emojiPanelButton.SetColor(true);
            characterCounter.gameObject.SetActive(true);

            InputBoxFocusChanged?.Invoke(true);
        }

        private void OnUICloseInput(InputAction.CallbackContext callbackContext)
        {
            if (suggestionPanel.IsActive)
            {
                suggestionPanelController!.SetPanelVisibility(false);
                lastMatch = Match.Empty;
                inputField.SelectInputField();
                return;
            }

            if (emojiPanel.gameObject.activeInHierarchy)
            {
                emojiPanelButton.SetState(false);
                emojiPanelController!.SetPanelVisibility(false);
                EmojiSelectionVisibilityChanged?.Invoke(false);
                inputField.SelectInputField();
                return;
            }

            inputField.DeactivateInputField();
            inputField.OnDeselect(null);
        }

        public void SubmitInputField()
        {
            if (suggestionPanel.IsActive)
            {
                //suggestionPanelController!.SetPanelVisibility(false);
                //lastMatch = Match.Empty;
                return;
            }

            if (emojiPanel.gameObject.activeInHierarchy)
            {
                emojiPanelButton.SetState(false);
                emojiPanelController!.SetPanelVisibility(false);
                EmojiSelectionVisibilityChanged?.Invoke(false);
            }

            string submittedText = inputField.text;

            if (!string.IsNullOrWhiteSpace(submittedText))
            {
                //TODO FRAN: Migrate this to CHAT CONTROLLER, as we dont know the channel here so we cant discriminate which sounds to play or not.
                if (chatSettings.chatAudioSettings == ChatAudioSettings.ALL)
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(chatSendMessageAudio);

                inputField.ResetInputField();

                InputSubmitted?.Invoke(submittedText, ORIGIN);
            }
        }

        public void Dispose()
        {
            if (emojiPanelController != null)
            {
                emojiPanelController.EmojiSelected -= OnEmojiSelected;
                emojiPanelController.Dispose();
            }

            if (suggestionPanelController != null)
            {
                suggestionPanelController.Dispose();
                suggestionPanelController.SuggestionSelected -= OnSuggestionSelected;
            }

            emojiPanelCts.SafeCancelAndDispose();

            inputField.Clicked -= OnClicked;
            inputField.PasteShortcutPerformed -= OnPasteShortcutPerformed;
        }

        private void OnSuggestionSelected(string suggestionId)
        {
            ReplaceSuggestionInText(suggestionId);
        }

        private void ReplaceSuggestionInText(string suggestion)
        {
            if (!lastMatch.Success || !inputField.IsWithinCharacterLimit(suggestion.Length - lastMatch.Groups[1].Length)) return;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(addEmojiAudio);
            int replaceAmount = lastMatch.Groups[1].Length;
            int replaceAt = wordMatchIndex + lastMatch.Groups[1].Index;

            inputField.ReplaceTextAtPosition(replaceAt, replaceAmount, suggestion);

            inputField.ActivateInputField();

            lastMatch = Match.Empty;
        }

        private void UpdateProfileNameMap()
        {
            GetParticipantProfiles(participantProfiles);

            var profileSuggestions = ListPool<KeyValuePair<string, ProfileInputSuggestionData>>.Get();
            profileSuggestions.AddRange(profileSuggestionsDictionary);

            for (var index = 0; index < profileSuggestions.Count; index++)
            {
                KeyValuePair<string, ProfileInputSuggestionData> suggestion = profileSuggestions[index];
                bool isThereProfileForSuggestion = participantProfiles.FindIndex((profile) => profile.UserId == suggestion.Value.GetId()) > -1;
                if (!isThereProfileForSuggestion)
                        profileSuggestionsDictionary.Remove(suggestion.Key);
            }

            profileSuggestions.Clear();
            ListPool<KeyValuePair<string, ProfileInputSuggestionData>>.Release(profileSuggestions);

            //We add or update the remaining participants
            foreach (Profile? profile in participantProfiles)
            {
                if (profile != null)
                {
                    if (profileSuggestionsDictionary.TryGetValue(profile.DisplayName, out ProfileInputSuggestionData profileSuggestionData))
                    {
                        if (profileSuggestionData.ProfileData != profile)
                            profileSuggestionsDictionary[profile.DisplayName] = new ProfileInputSuggestionData(profile, viewDependencies);
                    }
                    else
                    {
                        profileSuggestionsDictionary.TryAdd(profile.DisplayName, new ProfileInputSuggestionData(profile, viewDependencies));
                    }
                }
            }
        }

        private void InitializeEmojiPanelController()
        {
            emojiPanelController = new EmojiPanelController(emojiPanel, emojiPanelConfiguration, emojiMappingJson, emojiSectionViewPrefab, emojiButtonPrefab);
            emojiPanelController.EmojiSelected += OnEmojiSelected;
            emojiPanelButton.Button.onClick.AddListener(OnEmojiPanelButtonClicked);
        }

        private void OnEmojiPanelButtonClicked()
        {
            IsEmojiPanelVisible = !IsEmojiPanelVisible;
        }

        private void InitializeEmojiMapping(Dictionary<string, EmojiData> emojiNameDataMapping)
        {
            foreach ((string emojiName, EmojiData emojiData) in emojiNameDataMapping)
                emojiSuggestionsDictionary[emojiName] = new EmojiInputSuggestionData(emojiData.EmojiCode, emojiData.EmojiName);
        }


        private void OnEmojiSelected(string emoji)
        {
            AddEmojiToInput();
            return;

            void AddEmojiToInput()
            {
                UIAudioEventsBus.Instance.SendPlayAudioEvent(addEmojiAudio);
                if (!inputField.IsWithinCharacterLimit(emoji.Length)) return;
                inputField.InsertTextAtCaretPosition(emoji);
            }
        }
    }
}
