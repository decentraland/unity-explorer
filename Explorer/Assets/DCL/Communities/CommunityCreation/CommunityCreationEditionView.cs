using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using DCL.UI.SelectorButton;
using DCL.UI.Utilities;
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
        private const string PLACES_DROPDOWN_TITLE = "Select LAND or World";

        public Action? CancelButtonClicked;
        public Action? GetNameButtonClicked;
        public Action? SelectProfilePictureButtonClicked;
        public Action<string, string, List<string>, List<string>>? CreateCommunityButtonClicked;
        public Action<string, string, List<string>, List<string>>? SaveCommunityButtonClicked;
        public Action<int>? AddPlaceButtonClicked;
        public Action<int>? RemovePlaceButtonClicked;

        [SerializeField] public Button backgroundCloseButton = null!;

        [Header("Get Name Panel")]
        [SerializeField] private GameObject getNamePanel = null!;
        [SerializeField] private TMP_Text getNamePanelDescriptionText = null!;
        [SerializeField] private Button getNamePanelGetNameButton = null!;
        [SerializeField] private Button getNamePanelCancelButton = null!;
        [SerializeField] private AudioClipConfig clickOnLinksAudio = null!;

        [Header("Creation / Edition Panel")]
        [SerializeField] private GameObject creationPanel = null!;
        [SerializeField] private GameObject creationPanelContent = null!;
        [SerializeField] private GameObject creationPanelMainLoadingSpinner = null!;
        [SerializeField] private TMP_Text creationPanelTitleText = null!;
        [SerializeField] private ScrollRect creationPanelScrollRect = null!;
        [SerializeField] private Button creationPanelEditProfilePictureButton = null!;
        [SerializeField] private GameObject creationPanelProfilePictureIcon = null!;
        [SerializeField] private ImageView creationPanelProfileSelectedImage = null!;
        [SerializeField] private Sprite creationPanelProfileDefaultSelectedImage = null!;
        [SerializeField] private TMP_InputField creationPanelCommunityNameInputField = null!;
        [SerializeField] private GameObject creationPanelCommunityNameInputFieldOutline = null!;
        [SerializeField] private TMP_Text creationPanelCommunityNameCharCounter = null!;
        [SerializeField] private TMP_InputField creationPanelCommunityDescriptionInputField = null!;
        [SerializeField] private GameObject creationPanelCommunityDescriptionInputFieldOutline = null!;
        [SerializeField] private TMP_Text creationPanelCommunityDescriptionCharCounter = null!;
        [SerializeField] private SelectorButtonView creationPanelPlacesDropdown = null!;
        [SerializeField] private Transform placeTagsContainer = null!;
        [SerializeField] private CommunityPlaceTag placeTagPrefab = null!;
        [SerializeField] private Button creationPanelCancelButton = null!;
        [SerializeField] private Button creationPanelCreateButton = null!;
        [SerializeField] private TMP_Text creationPanelCreateButtonText = null!;
        [SerializeField] private GameObject creationPanelCreateButtonLoading = null!;

        [field: Header("Common")]
        [field: SerializeField] public WarningNotificationView WarningNotificationView { get; private set; } = null!;

        private readonly List<CommunityPlaceTag> currentPlaceTags = new();

        private bool isEditionMode;

        private CancellationTokenSource? updateScrollPositionCts;
        private CancellationTokenSource? thumbnailLoadingCts;

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
            creationPanelPlacesDropdown.OptionClicked += OnPlacesDropdownOptionSelected;
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
            creationPanelPlacesDropdown.OptionClicked -= OnPlacesDropdownOptionSelected;

            updateScrollPositionCts.SafeCancelAndDispose();
            thumbnailLoadingCts.SafeCancelAndDispose();
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

        public void SetProfileSelectedImage(string imageUrl, ThumbnailLoader thumbnailLoader)
        {
            creationPanelProfileSelectedImage.gameObject.SetActive(true);
            creationPanelProfilePictureIcon.SetActive(false);

            if (!string.IsNullOrEmpty(imageUrl))
            {
                thumbnailLoadingCts = thumbnailLoadingCts.SafeRestart();
                thumbnailLoader.LoadCommunityThumbnailAsync(imageUrl, creationPanelProfileSelectedImage, creationPanelProfileDefaultSelectedImage, thumbnailLoadingCts.Token).Forget();
            }
            else
            {
                creationPanelProfileSelectedImage.SetImage(creationPanelProfileDefaultSelectedImage);
            }
        }

        public void SetProfileSelectedImage(Sprite? sprite)
        {
            creationPanelProfileSelectedImage.gameObject.SetActive(sprite is not null);
            creationPanelProfilePictureIcon.SetActive(!creationPanelProfileSelectedImage.gameObject.activeSelf);
            creationPanelProfileSelectedImage.SetImage(sprite);
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
            creationPanelPlacesDropdown.SetMainButtonText(PLACES_DROPDOWN_TITLE);
            creationPanelPlacesDropdown.SetOptions(options);
        }

        private void OnPlacesDropdownOptionSelected(int index) =>
            AddPlaceButtonClicked?.Invoke(index);

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

            var dropdownOption = creationPanelPlacesDropdown.GetOption(placeName);
            dropdownOption?.SetHidden(true);

            if (updateScrollPosition)
            {
                updateScrollPositionCts = updateScrollPositionCts.SafeRestart();
                SetScrollPositionToBottomAsync(updateScrollPositionCts.Token).Forget();
            }
        }

        public void RemovePlaceTag(string id, string placeName)
        {
            currentPlaceTags.RemoveAll(placeTag =>
            {
                if (placeTag.Id != id)
                    return false;

                Destroy(placeTag.gameObject);

                var dropdownOption = creationPanelPlacesDropdown.GetOption(placeName);
                dropdownOption?.SetHidden(false);

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

        private void CreationPanelCommunityNameInputDeselected(string? _)
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

        private void CreationPanelCommunityDescriptionInputDeselected(string? _)
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
