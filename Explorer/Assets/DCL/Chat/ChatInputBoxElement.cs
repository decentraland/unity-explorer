using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Emoji;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.InputFieldValidator;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SuggestionPanel;
using MVC;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using TMPro;
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
        private static readonly Regex EMOJI_PATTERN_REGEX = new (@"(?<!https?:)(:\w{2,})", RegexOptions.Compiled);
        private static readonly Regex USERNAMES_PATTERN_REGEX = new (@"(?:^|\s)@([A-Za-z0-9]{1,15})(?=\s|$)", RegexOptions.Compiled);

        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private ValidatedInputFieldElement validatedInputField;
        [SerializeField] private CharacterCounterView characterCounter;
        [SerializeField] private RectTransform pastePopupPosition;

        [Header("Emojis")]
        [SerializeField] private EmojiPanelConfigurationSO emojiPanelConfiguration;
        [SerializeField] private EmojiButton emojiButtonPrefab;
        [SerializeField] private TextAsset emojiMappingJson;
        [SerializeField] private EmojiSectionView emojiSectionViewPrefab;
        [SerializeField] private EmojiButtonView emojiPanelButton;
        [SerializeField] private EmojiPanelView emojiPanel;

        [SerializeField] private InputSuggestionPanelElement suggestionPanel;

        [Header("Audio")]
        [SerializeField] private AudioClipConfig addEmojiAudio;
        [SerializeField] private AudioClipConfig addUserNameAudio;
        [SerializeField] private AudioClipConfig openEmojiPanelAudio;
        [SerializeField] private AudioClipConfig chatSendMessageAudio;
        [SerializeField] private AudioClipConfig chatInputTextAudio;
        [SerializeField] private AudioClipConfig enterInputAudio;

        private readonly List<EmojiData> emojiKeys = new ();
        private readonly List<ProfileInputSuggestionData> profileKeys = new ();

        private UniTaskCompletionSource closePopupTask;
        private Mouse device;
        private EmojiPanelController? emojiPanelController;
        public readonly Dictionary<string, ProfileInputSuggestionData> ProfileNameMapping = new ();
        private CancellationTokenSource emojiPanelCts = new ();
        private CancellationTokenSource searchCts = new ();
        private bool isInputSelected;
        private ViewDependencies viewDependencies;

        //THIS SHOULD NOT BE HERE!
        private IRoomHub roomHub;
        private IProfileNameColorHelper profileNameColorHelper;
        private IProfileRepository profileRepository;
        private string lastMatch;

        public string InputBoxText
        {
            get => validatedInputField.InputText;
            set => validatedInputField.SetText(value);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        /// <summary>
        ///     Raised when either the input box is selected or deselected.
        /// </summary>
        public event InputBoxSelectionChangedDelegate InputBoxSelectionChanged;

        /// <summary>
        ///     Raised when either the emoji selection panel opens or closes.
        /// </summary>
        public event EmojiSelectionVisibilityChangedDelegate EmojiSelectionVisibilityChanged;

        /// <summary>
        ///     Raised whenever the user attempts to send the content of the input box as a chat message.
        /// </summary>
        public event InputSubmittedDelegate InputSubmitted;

        /// <summary>
        ///     Raised whenever the input changes
        /// </summary>
        public event InputChangedDelegate InputChanged;

        public void Initialize(IRoomHub roomHub, IProfileNameColorHelper profileNameColorHelper, IProfileRepository profileRepository)
        {
            device = InputSystem.GetDevice<Mouse>();

            characterCounter.SetMaximumLength(validatedInputField.CharacterLimit);
            characterCounter.gameObject.SetActive(false);

            InitializeEmojiController();

            suggestionPanel.InjectDependencies(viewDependencies);
            suggestionPanel.Initialize();
            suggestionPanel.SuggestionSelectedEvent += OnSuggestionSelected;

            validatedInputField.InputFieldSelectionChanged += OnInputFieldSelectionChanged;
            validatedInputField.InputValidated += OnInputValidated;

            viewDependencies.DclInput.UI.RightClick.performed += OnRightClickRegistered;

            this.roomHub = roomHub;
            this.profileNameColorHelper = profileNameColorHelper;
            this.profileRepository = profileRepository;

            closePopupTask = new UniTaskCompletionSource();
        }

        /// <summary>
        ///     Makes the input box stop receiving user inputs.
        /// </summary>
        public void DisableInputBoxSubmissions()
        {
            viewDependencies.ClipboardManager.OnPaste -= PasteClipboardText;
            validatedInputField.InputFieldSubmit -= InputFieldSubmit;
            validatedInputField.DeactivateInputField();
        }

        public void EnableInputBoxSubmissions()
        {
            validatedInputField.InputFieldSubmit += InputFieldSubmit;
        }

        public void ClosePopups()
        {
            closePopupTask.TrySetResult();
        }

        public void FocusInputBox()
        {
            if (suggestionPanel.IsActive) return;

            if (validatedInputField.IsFocused) return;

            validatedInputField.SelectInputField();
        }

        private void OnInputValidated(string inputText)
        {
            HandleEmojiSearch(inputText);
            HandleUsernameSearch(inputText);
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
            if (validatedInputField.IsFocused)
                validatedInputField.SelectInputField(text);
        }

        /// <summary>
        ///     Makes the chat submit the current content of the input box.
        /// </summary>
        public void SubmitInput()
        {
            validatedInputField.SubmitInput(null);
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

                    suggestionPanel.SetPanelVisibility(false);
                }
            }
        }

        private void OnRightClickRegistered(InputAction.CallbackContext _)
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
                validatedInputField.ActivateInputField();
                InputChanged?.Invoke(validatedInputField.InputText);
            }
        }

        private void ToggleEmojiPanel()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(openEmojiPanelAudio);

            emojiPanelCts = emojiPanelCts.SafeRestart();
            bool toggle = !emojiPanel.gameObject.activeInHierarchy;
            emojiPanel.gameObject.SetActive(toggle);
            emojiPanelButton.SetState(toggle);
            suggestionPanel.SetPanelVisibility(false);
            emojiPanel.EmojiContainer.gameObject.SetActive(toggle);
            validatedInputField.ActivateInputField();

            EmojiSelectionVisibilityChanged?.Invoke(toggle);
        }

        private void PasteClipboardText(object sender, string pastedText)
        {
            validatedInputField.InsertTextAtSelectedPosition(pastedText);
            characterCounter.SetCharacterCount(validatedInputField.TextLength);
        }

        private void OnInputFieldSelectionChanged(bool isSelected)
        {
            if (isSelected) OnInputSelected();
            else OnInputDeselected();
        }

        private void OnInputDeselected()
        {
            isInputSelected = false;
            emojiPanelButton.SetColor(false);
            characterCounter.gameObject.SetActive(false);
            InputBoxSelectionChanged?.Invoke(false);
        }

        private void OnInputSelected()
        {
            InputBoxSelectionChanged?.Invoke(true);

            UIAudioEventsBus.Instance.SendPlayAudioEvent(enterInputAudio);

            if (isInputSelected) return;

            isInputSelected = true;
            emojiPanelButton.SetColor(true);
            characterCounter.gameObject.SetActive(true);
        }

        private void InputFieldSubmit(string _)
        {
            if (suggestionPanel.IsActive)
            {
                suggestionPanel.SetPanelVisibility(false);
                return;
            }

            if (emojiPanel.gameObject.activeInHierarchy)
            {
                emojiPanelButton.SetState(false);
                emojiPanelController!.SetPanelVisibility(false);
                EmojiSelectionVisibilityChanged?.Invoke(false);
            }

            if (string.IsNullOrWhiteSpace(validatedInputField.InputText))
            {
                validatedInputField.DeactivateInputField();
                validatedInputField.DeselectInputField();
                return;
            }

            //Send message and clear Input Field
            UIAudioEventsBus.Instance.SendPlayAudioEvent(chatSendMessageAudio);
            string messageToSend = validatedInputField.InputText;

            validatedInputField.ResetInputField();

            InputSubmitted?.Invoke(messageToSend, ORIGIN);
        }

        public void Dispose()
        {
            if (emojiPanelController != null)
            {
                emojiPanelController.EmojiSelected -= AddEmojiToInput;
                emojiPanelController.Dispose();
            }

            suggestionPanel.SuggestionSelectedEvent -= OnSuggestionSelected;

            emojiPanelCts.SafeCancelAndDispose();
            searchCts.SafeCancelAndDispose();

            viewDependencies.DclInput.UI.RightClick.performed -= OnRightClickRegistered;
        }

        private void OnSuggestionSelected(string suggestionId, InputSuggestionType suggestionType, bool shouldClose)
        {
            switch (suggestionType)
            {
                case InputSuggestionType.NONE: break;
                case InputSuggestionType.EMOJIS:
                    AddEmojiFromSuggestion(suggestionId, shouldClose);
                    break;
                case InputSuggestionType.PROFILE:
                    AddUsernameFromSuggestion(suggestionId, shouldClose);
                    break;
            }
        }

        private void AddUsernameFromSuggestion(string username, bool shouldClose)
        {
            if (!validatedInputField.IsWithinCharacterLimit(username.Length)) return;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(addUserNameAudio);

            inputField.SetTextWithoutNotify(inputField.text.Replace(lastMatch, username));
            inputField.stringPosition += username.Length;

            validatedInputField.ActivateInputField();

            if (shouldClose)
                suggestionPanel.SetPanelVisibility(false);
        }

        private void HandleUsernameSearch(string inputText)
        {
            Match match = USERNAMES_PATTERN_REGEX.Match(inputText);

            if (match.Success)
            {
                lastMatch = match.Groups[1].Value;
                searchCts.SafeCancelAndDispose();
                searchCts = new CancellationTokenSource();
                SearchAndSetUsernameSuggestionsAsync(lastMatch, searchCts.Token).Forget();
            }
            else
            {
                if (suggestionPanel.IsActive)
                    suggestionPanel.SetPanelVisibility(false);
            }
        }

        private async UniTaskVoid SearchAndSetUsernameSuggestionsAsync(string value, CancellationToken ct)
        {
            //TODO FRAN: Make this more general so all types of suggestions can reuse the same code
            await UpdateProfileNameMapping(ct);

            await DictionaryUtils.GetKeysWithPrefixAsync(ProfileNameMapping, value, profileKeys, ct);

            var suggestions = new List<ISuggestionElementData>();

            foreach (var data in profileKeys)
                suggestions.Add(data);

            suggestionPanel.SetSuggestionValues(InputSuggestionType.PROFILE, suggestions);
            suggestionPanel.SetPanelVisibility(true);
        }

        private async UniTask UpdateProfileNameMapping(CancellationToken ct)
        {
            //TODO FRAN: Fix this so it doesnt run every time we put one letter, this should be filled and updated regularly, but not every time.
            var remoteParticipantIdentities = roomHub.IslandRoom().Participants.RemoteParticipantIdentities();

            ProfileNameMapping.Clear();
            foreach (var participant in remoteParticipantIdentities)
            {
                await ExecuteAsync();

                async UniTask ExecuteAsync()
                {
                    Profile? profile = await profileRepository.GetAsync(participant, ct);
                    var color = profileNameColorHelper.GetNameColor(profile.Name);
                    ProfileNameMapping.TryAdd(profile.DisplayName, new ProfileInputSuggestionData(profile, color));
                }
            }
        }

#region Emojis
        private void InitializeEmojiController()
        {
            emojiPanelController = new EmojiPanelController(emojiPanel, emojiPanelConfiguration, emojiMappingJson, emojiSectionViewPrefab, emojiButtonPrefab);
            emojiPanelController.EmojiSelected += AddEmojiToInput;
            emojiPanelButton.Button.onClick.AddListener(ToggleEmojiPanel);
        }

        private void AddEmojiFromSuggestion(string emojiCode, bool shouldClose)
        {
            if (!validatedInputField.IsWithinCharacterLimit(emojiCode.Length)) return;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(addEmojiAudio);

            inputField.SetTextWithoutNotify(inputField.text.Replace(EMOJI_PATTERN_REGEX.Match(validatedInputField.InputText).Value, emojiCode));
            inputField.stringPosition += emojiCode.Length;

            validatedInputField.ActivateInputField();

            if (shouldClose)
                suggestionPanel.SetPanelVisibility(false);
        }

        private void AddEmojiToInput(string emoji)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(addEmojiAudio);

            if (!validatedInputField.IsWithinCharacterLimit(emoji.Length)) return;

            validatedInputField.InsertTextAtSelectedPosition(emoji);
        }

        private void HandleEmojiSearch(string inputText)
        {
            Match match = EMOJI_PATTERN_REGEX.Match(inputText);

            if (match.Success)
            {
                if (match.Value.Length < 2)
                {
                    suggestionPanel.SetPanelVisibility(false);
                    return;
                }

                searchCts.SafeCancelAndDispose();
                searchCts = new CancellationTokenSource();

                SearchAndSetEmojiSuggestionsAsync(match.Value, searchCts.Token).Forget();
            }
            else
            {
                if (suggestionPanel.IsActive)
                    suggestionPanel.SetPanelVisibility(false);
            }
        }

        private async UniTaskVoid SearchAndSetEmojiSuggestionsAsync(string value, CancellationToken ct)
        {
            await DictionaryUtils.GetKeysWithPrefixAsync(emojiPanelController!.EmojiNameMapping, value, emojiKeys, ct);

            var suggestions = new List<ISuggestionElementData>();

            foreach (EmojiData emojiData in emojiKeys)
                suggestions.Add(new EmojiInputSuggestionData(emojiData.EmojiCode, emojiData.EmojiName));

            suggestionPanel.SetSuggestionValues(InputSuggestionType.EMOJIS, suggestions);
            suggestionPanel.SetPanelVisibility(true);
        }
#endregion
    }
}
