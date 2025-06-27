using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using DCL.UI.Utilities;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityCreationEditionView : ViewBase, IView
    {
        private const string CREATE_COMMUNITY_TITLE = "Create a Community";
        private const string EDIT_COMMUNITY_TITLE = "Edit Community";
        private const string DEFAULT_PLACES_DROPDOWN_OPTION = "-- Select an option --";

        public Action CancelButtonClicked;
        public Action GetNameButtonClicked;
        public Action SelectProfilePictureButtonClicked;
        public Action<string, string, List<string>, List<string>> CreateCommunityButtonClicked;
        public Action<string, string, List<string>, List<string>> SaveCommunityButtonClicked;
        public Action<int> AddPlaceButtonClicked;
        public Action<int> RemovePlaceButtonClicked;

        [SerializeField] public Button backgroundCloseButton;

        [Header("Get Name Panel")]
        [SerializeField] private GameObject getNamePanel;
        [SerializeField] private TMP_Text getNamePanelDescriptionText;
        [SerializeField] private Button getNamePanelGetNameButton;
        [SerializeField] private Button getNamePanelCancelButton;
        [SerializeField] private AudioClipConfig clickOnLinksAudio;

        [Header("Creation / Edition Panel")]
        [SerializeField] private GameObject creationPanel;
        [SerializeField] private GameObject creationPanelContent;
        [SerializeField] private GameObject creationPanelMainLoadingSpinner;
        [SerializeField] private TMP_Text creationPanelTitleText;
        [SerializeField] private ScrollRect creationPanelScrollRect;
        [SerializeField] private Button creationPanelEditProfilePictureButton;
        [SerializeField] private ImageView creationPanelProfileSelectedImage;
        [SerializeField] private Sprite creationPanelProfileDefaultSelectedImage;
        [SerializeField] private TMP_InputField creationPanelCommunityNameInputField;
        [SerializeField] private GameObject creationPanelCommunityNameInputFieldOutline;
        [SerializeField] private TMP_Text creationPanelCommunityNameCharCounter;
        [SerializeField] private TMP_InputField creationPanelCommunityDescriptionInputField;
        [SerializeField] private GameObject creationPanelCommunityDescriptionInputFieldOutline;
        [SerializeField] private TMP_Text creationPanelCommunityDescriptionCharCounter;
        [SerializeField] private TMP_Dropdown creationPanelPlacesDropdown;
        [SerializeField] private Button creationPanelAddPlaceButton;
        [SerializeField] private Transform placeTagsContainer;
        [SerializeField] private CommunityPlaceTag placeTagPrefab;
        [SerializeField] private Button creationPanelCancelButton;
        [SerializeField] private Button creationPanelCreateButton;
        [SerializeField] private TMP_Text creationPanelCreateButtonText;
        [SerializeField] private GameObject creationPanelCreateButtonLoading;

        [field: Header("Common")]
        [field: SerializeField] public WarningNotificationView WarningNotificationView { get; private set; }

        private readonly List<CommunityPlaceTag> currentPlaceTags = new();

        private ImageController imageController;
        private bool isEditionMode;
        private bool isDefaultImageSelected;

        private CancellationTokenSource updateScrollPositionCts;

        private void Awake()
        {
            creationPanelScrollRect.SetScrollSensitivityBasedOnPlatform();
            getNamePanelCancelButton.onClick.AddListener(() => CancelButtonClicked?.Invoke());
            getNamePanelGetNameButton.onClick.AddListener(() => GetNameButtonClicked?.Invoke());
            creationPanelCancelButton.onClick.AddListener(() => CancelButtonClicked?.Invoke());
            creationPanelEditProfilePictureButton.onClick.AddListener(() => SelectProfilePictureButtonClicked?.Invoke());
            creationPanelCommunityNameInputField.onValueChanged.AddListener(CreationPanelCommunityNameInputChanged);
            creationPanelCommunityNameInputField.onSelect.AddListener(CreationPanelCommunityNameInputSelected);
            creationPanelCommunityNameInputField.onDeselect.AddListener(CreationPanelCommunityNameInputDeselected);
            creationPanelCommunityDescriptionInputField.onValueChanged.AddListener(CreationPanelCommunityDescriptionInputChanged);
            creationPanelCommunityDescriptionInputField.onSelect.AddListener(CreationPanelCommunityDescriptionInputSelected);
            creationPanelCommunityDescriptionInputField.onDeselect.AddListener(CreationPanelCommunityDescriptionInputDeselected);
            creationPanelCreateButton.onClick.AddListener(() =>
            {
                var lands = new List<string>();
                var worlds = new List<string>();
                foreach (CommunityPlaceTag placeTag in currentPlaceTags)
                {
                    if (placeTag.IsWorld)
                        worlds.Add(placeTag.Id);
                    else
                        lands.Add(placeTag.Id);
                }

                if (!isEditionMode)
                    CreateCommunityButtonClicked?.Invoke(
                        creationPanelCommunityNameInputField.text,
                        creationPanelCommunityDescriptionInputField.text,
                        lands,
                        worlds);
                else
                    SaveCommunityButtonClicked?.Invoke(
                        creationPanelCommunityNameInputField.text,
                        creationPanelCommunityDescriptionInputField.text,
                        lands,
                        worlds);
            });
            creationPanelPlacesDropdown.onValueChanged.AddListener(index => creationPanelAddPlaceButton.interactable = index > 0);
            creationPanelAddPlaceButton.onClick.AddListener(() => AddPlaceButtonClicked?.Invoke(creationPanelPlacesDropdown.value - 1)); // The first option is the default one, so we need to subtract 1 to the index
        }

        private void OnDestroy()
        {
            getNamePanelCancelButton.onClick.RemoveAllListeners();
            getNamePanelGetNameButton.onClick.RemoveAllListeners();
            creationPanelCancelButton.onClick.RemoveAllListeners();
            creationPanelEditProfilePictureButton.onClick.RemoveAllListeners();
            creationPanelCommunityNameInputField.onValueChanged.RemoveAllListeners();
            creationPanelCommunityNameInputField.onSelect.RemoveAllListeners();
            creationPanelCommunityNameInputField.onDeselect.RemoveAllListeners();
            creationPanelCommunityDescriptionInputField.onValueChanged.RemoveAllListeners();
            creationPanelCommunityDescriptionInputField.onSelect.RemoveAllListeners();
            creationPanelCommunityDescriptionInputField.onDeselect.RemoveAllListeners();
            creationPanelPlacesDropdown.onValueChanged.RemoveAllListeners();

            updateScrollPositionCts.SafeCancelAndDispose();
        }

        public void SetCreationPanelAsLoading(bool isLoading)
        {
            creationPanelMainLoadingSpinner.SetActive(isLoading);
            creationPanelContent.SetActive(!isLoading);
        }

        public void SetAccess(bool canCreate)
        {
            getNamePanel.SetActive(!canCreate);
            creationPanel.SetActive(canCreate);

            if (canCreate)
                CleanCreationPanel();

            creationPanelScrollRect.verticalNormalizedPosition = 1f;
            WarningNotificationView.Hide(true);
        }

        public void ConvertGetNameDescriptionUrlsToClickableLinks(Action<string> onLinkClicked) =>
            getNamePanelDescriptionText.ConvertUrlsToClickeableLinks(onLinkClicked);

        public void PlayOnLinkClickAudio() =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(clickOnLinksAudio);

        public void SetAsEditionMode(bool isEditMode)
        {
            isEditionMode = isEditMode;
            creationPanelTitleText.text = isEditMode ? EDIT_COMMUNITY_TITLE : CREATE_COMMUNITY_TITLE;
            creationPanelCreateButtonText.text = isEditMode ? "SAVE" : "CREATE";
        }

        public void SetCommunityCreationInProgress(bool isInProgress)
        {
            creationPanelCreateButtonLoading.SetActive(isInProgress);
            creationPanelCreateButtonText.gameObject.SetActive(!isInProgress);

            if (isInProgress)
                creationPanelCreateButton.interactable = false;
            else
                UpdateCreateButtonAvailability();
        }

        public void ConfigureImageController(IWebRequestController webRequestController)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(creationPanelProfileSelectedImage, webRequestController);
        }

        public void SetProfileSelectedImage(string imageUrl)
        {
            isDefaultImageSelected = false;
            creationPanelProfileSelectedImage.gameObject.SetActive(true);

            if (!string.IsNullOrEmpty(imageUrl))
                imageController?.RequestImage(imageUrl, hideImageWhileLoading: true);
            else
            {
                imageController.SetImage(creationPanelProfileDefaultSelectedImage);
                isDefaultImageSelected = true;
            }
        }

        public void SetProfileSelectedImage(Sprite sprite)
        {
            isDefaultImageSelected = false;
            creationPanelProfileSelectedImage.gameObject.SetActive(sprite is not null);
            imageController.SetImage(sprite);
        }

        public void SetCommunityName(string text, bool isInteractable)
        {
            creationPanelCommunityNameInputField.text = text;
            creationPanelCommunityNameInputField.interactable = isInteractable;
            UpdateCreateButtonAvailability();
        }

        public void SetCommunityDescription(string text)
        {
            creationPanelCommunityDescriptionInputField.text = text;
            UpdateCreateButtonAvailability();
        }

        public void SetPlacesSelector(List<string> options)
        {
            creationPanelPlacesDropdown.ClearOptions();
            creationPanelPlacesDropdown.options.Add(new TMP_Dropdown.OptionData(DEFAULT_PLACES_DROPDOWN_OPTION));
            creationPanelAddPlaceButton.interactable = false;

            if (options.Count > 0)
            {
                creationPanelPlacesDropdown.AddOptions(options);
                creationPanelPlacesDropdown.value = 0;
            }
        }
        public void AddPlaceTag(string id, bool isWorld, string placeName, bool isRemovalAllowed, bool updateScrollPosition = true)
        {
            CommunityPlaceTag placeTag = Instantiate(placeTagPrefab, placeTagsContainer);
            placeTag.Setup(id, isWorld, placeName, isRemovalAllowed);

            void OnPlaceTagRemovedClicked()
            {
                if (!currentPlaceTags.Contains(placeTag))
                    return;

                RemovePlaceButtonClicked?.Invoke(currentPlaceTags.IndexOf(placeTag));
            }

            placeTag.RemoveButtonClicked -= OnPlaceTagRemovedClicked;
            placeTag.RemoveButtonClicked += OnPlaceTagRemovedClicked;

            currentPlaceTags.Add(placeTag);
            creationPanelPlacesDropdown.value = 0;

            if (updateScrollPosition)
            {
                updateScrollPositionCts = updateScrollPositionCts.SafeRestart();
                SetScrollPositionToBottomAsync(updateScrollPositionCts.Token).Forget();
            }
        }

        public void RemovePlaceTag(string id)
        {
            currentPlaceTags.RemoveAll(placeTag =>
            {
                if (placeTag.Id != id)
                    return false;

                Destroy(placeTag.gameObject);
                return true;
            });

            updateScrollPositionCts = updateScrollPositionCts.SafeRestart();
            SetScrollPositionToBottomAsync(updateScrollPositionCts.Token).Forget();
        }

        public void CleanCreationPanel()
        {
            SetCommunityCreationInProgress(false);
            SetProfileSelectedImage(sprite: null);
            SetCommunityName(string.Empty, true);
            SetCommunityDescription(string.Empty);
            SetPlacesSelector(new List<string>());
            CreationPanelCommunityNameInputDeselected(null);
            CreationPanelCommunityDescriptionInputDeselected(null);

            foreach (CommunityPlaceTag placeTag in currentPlaceTags)
                Destroy(placeTag.gameObject);
            currentPlaceTags.Clear();
        }

        private async UniTaskVoid SetScrollPositionToBottomAsync(CancellationToken ct)
        {
            await UniTask.DelayFrame(1, cancellationToken: ct);
            creationPanelScrollRect.verticalNormalizedPosition = 0f;
        }

        private void CreationPanelCommunityNameInputChanged(string text)
        {
            creationPanelCommunityNameCharCounter.text = $"{text.Length}/{creationPanelCommunityNameInputField.characterLimit}";
            UpdateCreateButtonAvailability();
        }

        private void CreationPanelCommunityNameInputSelected(string _)
        {
            creationPanelCommunityNameCharCounter.gameObject.SetActive(true);
            creationPanelCommunityNameInputFieldOutline.SetActive(true);
        }

        private void CreationPanelCommunityNameInputDeselected(string _)
        {
            creationPanelCommunityNameCharCounter.gameObject.SetActive(false);
            creationPanelCommunityNameInputFieldOutline.SetActive(false);
        }

        private void CreationPanelCommunityDescriptionInputChanged(string text)
        {
            creationPanelCommunityDescriptionCharCounter.text = $"{text.Length}/{creationPanelCommunityDescriptionInputField.characterLimit}";
            UpdateCreateButtonAvailability();
        }

        private void CreationPanelCommunityDescriptionInputSelected(string _)
        {
            creationPanelCommunityDescriptionInputFieldOutline.SetActive(true);
            creationPanelCommunityDescriptionCharCounter.gameObject.SetActive(true);
        }

        private void CreationPanelCommunityDescriptionInputDeselected(string _)
        {
            creationPanelCommunityDescriptionInputFieldOutline.SetActive(false);
            creationPanelCommunityDescriptionCharCounter.gameObject.SetActive(false);
        }

        private void UpdateCreateButtonAvailability()
        {
            creationPanelCreateButton.interactable =
                !string.IsNullOrEmpty(creationPanelCommunityNameInputField.text) &&
                !string.IsNullOrEmpty(creationPanelCommunityDescriptionInputField.text);
        }
    }
}
