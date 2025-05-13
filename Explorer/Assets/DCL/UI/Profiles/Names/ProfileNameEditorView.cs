using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ProfileNames
{
    public class ProfileNameEditorView : ViewBase, IView
    {
        [field: SerializeField]
        public NonClaimedNameConfig NonClaimedNameContainer { get; set; }

        [field: SerializeField]
        public ClaimedNameConfig ClaimedNameContainer { get; set; }

        [field: SerializeField]
        public Button OverlayCloseButton { get; set; }

        [Serializable]
        public struct NonClaimedNameConfig
        {
            public GameObject root;
            public TMP_InputField input;
            public Image inputOutline;
            public TMP_Text inputErrorMessage;
            public GameObject errorContainer;
            public TMP_Text characterCountLabel;
            public TMP_Text userHashLabel;
            public Button saveButton;
            public GameObject saveLoading;
            public TMP_Text saveButtonText;
            public Button cancelButton;
            public Button claimNameButton;

            public bool saveButtonInteractable
            {
                set
                {
                    saveButton.interactable = value;

                    saveButtonText.color = value
                        ? new Color(0.99f, 0.99f, 0.99f)
                        : new Color(0.99f, 0.99f, 0.99f, 0.5f);
                }
            }
        }

        [Serializable]
        public struct ClaimedNameConfig
        {
            public TabHeaderOption NonClaimedNameTabHeader;
            public TabHeaderOption ClaimedNameTabHeader;
            public NonClaimedNameConfig NonClaimedNameTabConfig;

            public Button saveButton;
            public GameObject saveLoading;
            public TMP_Text saveButtonText;
            public Button cancelButton;
            public TMP_Dropdown claimedNameDropdown;
            public GameObject dropdownLoadingSpinner;
            public GameObject dropdownVerifiedIcon;
            public TMP_Text_ClickeableLink clickeableLink;

            public bool saveButtonInteractable
            {
                set
                {
                    saveButton.interactable = value;

                    const float RGB = 0.99f;

                    saveButtonText.color = value
                        ? new Color(RGB, RGB, RGB)
                        : new Color(RGB, RGB, RGB, 0.5f);
                }
            }

            [Serializable]
            public struct TabHeaderOption
            {
                public Button SelectButton;
                public GameObject SelectedContainer;
                public GameObject NonSelectedContainer;
                public GameObject Content;

                public void Select()
                {
                    SelectedContainer.SetActive(true);
                    NonSelectedContainer.SetActive(false);
                    Content.SetActive(true);
                }

                public void Deselect()
                {
                    SelectedContainer.SetActive(false);
                    NonSelectedContainer.SetActive(true);
                    Content.SetActive(false);
                }
            }
        }
    }
}
