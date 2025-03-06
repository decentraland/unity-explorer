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

        [Serializable]
        public struct NonClaimedNameConfig
        {
            public GameObject root;
            public TMP_InputField input;
            public TMP_Text characterCountLabel;
            public TMP_Text userHashLabel;
            public Button saveButton;
            public Button cancelButton;
            public Button claimNameButton;
        }

        [Serializable]
        public struct ClaimedNameConfig
        {
            public TabHeaderOption NonClaimedNameTabHeader;
            public TabHeaderOption ClaimedNameTabHeader;
            public NonClaimedNameConfig NonClaimedNameTabConfig;

            public Button saveButton;
            public Button cancelButton;
            public TMP_Dropdown claimedNameDropdown;

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
