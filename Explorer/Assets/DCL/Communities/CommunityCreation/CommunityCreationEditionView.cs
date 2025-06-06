using DCL.Audio;
using DCL.UI;
using DCL.UI.Utilities;
using MVC;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityCreationEditionView : ViewBase, IView
    {
        private const string DEFAULT_PLACES_DROPDOWN_OPTION = "-- Select an option --";

        public Action CancelButtonClicked;
        public Action GetNameButtonClicked;
        public Action SelectProfilePictureButtonClicked;
        public Action<string, string> CreateCommunityButtonClicked;
        public Action<int> AddPlaceButtonClicked;

        [SerializeField] public Button backgroundCloseButton;

        [Header("Get Name Panel")]
        [SerializeField] private GameObject getNamePanel;
        [SerializeField] private TMP_Text getNamePanelDescriptionText;
        [SerializeField] private Button getNamePanelGetNameButton;
        [SerializeField] private Button getNamePanelCancelButton;
        [SerializeField] private AudioClipConfig clickOnLinksAudio;

        [Header("Creation / Edition Panel")]
        [SerializeField] private GameObject creationPanel;
        [SerializeField] private TMP_Text creationPanelTitleText;
        [SerializeField] private ScrollRect creationPanelScrollRect;
        [SerializeField] private Button creationPanelEditProfilePictureButton;
        [SerializeField] private Image creationPanelProfileSelectedImage;
        [SerializeField] private TMP_InputField creationPanelCommunityNameInputField;
        [SerializeField] private TMP_Text creationPanelCommunityNameCharCounter;
        [SerializeField] private TMP_InputField creationPanelCommunityDescriptionInputField;
        [SerializeField] private TMP_Text creationPanelCommunityDescriptionCharCounter;
        [SerializeField] private TMP_Dropdown creationPanelPlacesDropdown;
        [SerializeField] private Button creationPanelAddPlaceButton;
        [SerializeField] private Transform placeTagsContainer;
        [SerializeField] private GameObject placeTagPrefab;
        [SerializeField] private Button creationPanelCancelButton;
        [SerializeField] private Button creationPanelCreateButton;
        [SerializeField] private GameObject creationPanelCreateButtonText;
        [SerializeField] private GameObject creationPanelCreateLoading;

        private readonly List<GameObject> currentPlaceTags = new();

        private void Awake()
        {
            creationPanelScrollRect.SetScrollSensitivityBasedOnPlatform();
            getNamePanelCancelButton.onClick.AddListener(() => CancelButtonClicked?.Invoke());
            getNamePanelGetNameButton.onClick.AddListener(() => GetNameButtonClicked?.Invoke());
            creationPanelCancelButton.onClick.AddListener(() => CancelButtonClicked?.Invoke());
            creationPanelEditProfilePictureButton.onClick.AddListener(() => SelectProfilePictureButtonClicked?.Invoke());
            creationPanelCommunityNameInputField.onValueChanged.AddListener(CreationPanelCommunityNameInputChanged);
            creationPanelCommunityDescriptionInputField.onValueChanged.AddListener(CreationPanelCommunityDescriptionInputChanged);
            creationPanelCreateButton.onClick.AddListener(() => CreateCommunityButtonClicked?.Invoke(
                creationPanelCommunityNameInputField.text,
                creationPanelCommunityDescriptionInputField.text));
            creationPanelPlacesDropdown.onValueChanged.AddListener(index => creationPanelAddPlaceButton.interactable = index > 0);
            creationPanelAddPlaceButton.onClick.AddListener(() => AddPlaceButtonClicked?.Invoke(creationPanelPlacesDropdown.value));
        }

        private void OnDestroy()
        {
            getNamePanelCancelButton.onClick.RemoveAllListeners();
            getNamePanelGetNameButton.onClick.RemoveAllListeners();
            creationPanelCancelButton.onClick.RemoveAllListeners();
            creationPanelEditProfilePictureButton.onClick.RemoveAllListeners();
            creationPanelCommunityNameInputField.onValueChanged.RemoveAllListeners();
            creationPanelCommunityDescriptionInputField.onValueChanged.RemoveAllListeners();
            creationPanelPlacesDropdown.onValueChanged.RemoveAllListeners();
        }

        public void SetAccess(bool canCreate)
        {
            getNamePanel.SetActive(!canCreate);
            creationPanel.SetActive(canCreate);

            if (canCreate)
                CleanCreationPanel();
        }

        public void ConvertGetNameDescriptionUrlsToClickableLinks(Action<string> onLinkClicked) =>
            getNamePanelDescriptionText.ConvertUrlsToClickeableLinks(onLinkClicked);

        public void PlayOnLinkClickAudio() =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(clickOnLinksAudio);

        public void SetCreationPanelTitle(string title) =>
            creationPanelTitleText.text = title;

        public void SetCreationPanelAsLoading(bool isLoading)
        {
            creationPanelCreateLoading.SetActive(isLoading);
            creationPanelCreateButtonText.SetActive(!isLoading);

            if (isLoading)
                creationPanelCreateButton.interactable = false;
            else
                CheckForCreateButtonAvailability();
        }

        public void SetProfileSelectedImage(Sprite sprite)
        {
            creationPanelProfileSelectedImage.gameObject.SetActive(sprite is not null);
            creationPanelProfileSelectedImage.sprite = sprite;
        }

        public void SetCommunityName(string text)
        {
            creationPanelCommunityNameInputField.text = text;
            CheckForCreateButtonAvailability();
        }

        public void SetCommunityDescription(string text)
        {
            creationPanelCommunityDescriptionInputField.text = text;
            CheckForCreateButtonAvailability();
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
        public void AddPlaceTag(string placeName)
        {
            GameObject placeTag = Instantiate(placeTagPrefab, placeTagsContainer);
            placeTag.GetComponentInChildren<TMP_Text>().text = placeName;
            placeTag.GetComponentInChildren<Button>().onClick.AddListener(() =>
            {
                if (!currentPlaceTags.Contains(placeTag))
                    return;

                currentPlaceTags.Remove(placeTag);
                Destroy(placeTag);
            });
            currentPlaceTags.Add(placeTag);
            creationPanelScrollRect.verticalNormalizedPosition = 0f;
        }

        private void CleanCreationPanel()
        {
            SetCreationPanelAsLoading(false);
            SetProfileSelectedImage(null);
            SetCommunityName(string.Empty);
            SetCommunityDescription(string.Empty);
            SetPlacesSelector(new List<string>());

            foreach (GameObject placeTag in currentPlaceTags)
                Destroy(placeTag);
            currentPlaceTags.Clear();

            creationPanelScrollRect.verticalNormalizedPosition = 1f;
        }

        private void CreationPanelCommunityNameInputChanged(string text)
        {
            creationPanelCommunityNameCharCounter.text = $"{text.Length}/{creationPanelCommunityNameInputField.characterLimit}";
            CheckForCreateButtonAvailability();
        }

        private void CreationPanelCommunityDescriptionInputChanged(string text)
        {
            creationPanelCommunityDescriptionCharCounter.text = $"{text.Length}/{creationPanelCommunityDescriptionInputField.characterLimit}";
            CheckForCreateButtonAvailability();
        }

        private void CheckForCreateButtonAvailability()
        {
            creationPanelCreateButton.interactable =
                !string.IsNullOrEmpty(creationPanelCommunityNameInputField.text) &&
                !string.IsNullOrEmpty(creationPanelCommunityDescriptionInputField.text);
        }
    }
}
