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
        public Action CancelButtonClicked;
        public Action GetNameButtonClicked;
        public Action SelectProfilePictureButtonClicked;

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
        [SerializeField] private Button creationPanelCancelButton;
        [SerializeField] private Button creationPanelCreateButton;

        private void Awake()
        {
            creationPanelScrollRect.SetScrollSensitivityBasedOnPlatform();
            getNamePanelCancelButton.onClick.AddListener(() => CancelButtonClicked?.Invoke());
            getNamePanelGetNameButton.onClick.AddListener(() => GetNameButtonClicked?.Invoke());
            creationPanelCancelButton.onClick.AddListener(() => CancelButtonClicked?.Invoke());
            creationPanelEditProfilePictureButton.onClick.AddListener(() => SelectProfilePictureButtonClicked?.Invoke());
            creationPanelCommunityNameInputField.onValueChanged.AddListener(CreationPanelCommunityNameInputChanged);
            creationPanelCommunityDescriptionInputField.onValueChanged.AddListener(CreationPanelCommunityDescriptionInputChanged);
        }

        private void OnDestroy()
        {
            getNamePanelCancelButton.onClick.RemoveAllListeners();
            getNamePanelGetNameButton.onClick.RemoveAllListeners();
            creationPanelCancelButton.onClick.RemoveAllListeners();
            creationPanelEditProfilePictureButton.onClick.RemoveAllListeners();
            creationPanelCommunityNameInputField.onValueChanged.RemoveAllListeners();
            creationPanelCommunityDescriptionInputField.onValueChanged.RemoveAllListeners();
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
            if (options.Count > 0)
            {
                creationPanelPlacesDropdown.AddOptions(options);
                creationPanelPlacesDropdown.value = 0;
            }
        }

        private void CleanCreationPanel()
        {
            SetProfileSelectedImage(null);
            SetCommunityName(string.Empty);
            SetCommunityDescription(string.Empty);
            SetPlacesSelector(new List<string>());
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
