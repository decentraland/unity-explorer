using DCL.Passport.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Fields
{
    public class AdditionalField_PassportFieldView : MonoBehaviour
    {
        [field: SerializeField]
        public Image Logo { get; private set; }

        [field: SerializeField]
        public TMP_Text Title { get; private set; }

        [field: SerializeField]
        public TMP_Text Value { get; private set; }

        [field: SerializeField]
        public TMP_Dropdown EditionDropdown { get; private set; }

        [field: SerializeField]
        public TMP_InputField EditionTextInput { get; private set; }

        [field: SerializeField]
        public TMP_Text EditionTextInputPlaceholder { get; private set; }

        [field: SerializeField]
        public Button CancelEditionTextInputButton { get; private set; }

        [field: SerializeField]
        public bool IsEditableWithDropdown { get; set; }

        [field: SerializeField]
        public AdditionalFieldType Type { get; set; }

        private bool isInEditMode;

        private void Awake()
        {
            EditionDropdown.onValueChanged.AddListener(index =>
            {
                if (index == 0)
                    EditionTextInput.text = string.Empty;
                else if (index > 0 && index < EditionDropdown.options.Count - 1)
                    EditionTextInput.text = EditionDropdown.options[index].text;
                else
                {
                    EditionDropdown.gameObject.SetActive(false);
                    EditionTextInput.gameObject.SetActive(true);
                    EditionTextInput.text = string.Empty;
                    EditionTextInput.Select();
                    EditionTextInput.ActivateInputField();
                }
            });

            CancelEditionTextInputButton.onClick.AddListener(() =>
            {
                EditionDropdown.gameObject.SetActive(true);
                EditionDropdown.value = 0;
                EditionTextInput.gameObject.SetActive(false);
                EditionTextInput.text = string.Empty;
            });
        }

        private void OnDestroy() =>
            EditionDropdown.onValueChanged.RemoveAllListeners();

        public void SetAsEditable(bool isEditable)
        {
            Value.gameObject.SetActive(!isEditable);
            EditionDropdown.gameObject.SetActive(isEditable && IsEditableWithDropdown);
            EditionTextInput.gameObject.SetActive(isEditable && !IsEditableWithDropdown);
            CancelEditionTextInputButton.gameObject.SetActive(isEditable && IsEditableWithDropdown);
            isInEditMode = isEditable;
        }

        public void SetEditionValue(string value)
        {
            var selectedIndex = 0;
            if (!string.IsNullOrEmpty(value))
            {
                for (var i = 1; i < EditionDropdown.options.Count; i++)
                {
                    selectedIndex = i;
                    var option = EditionDropdown.options[i];
                    if (option.text != value) continue;
                    break;
                }
            }

            EditionDropdown.value = selectedIndex;
            EditionTextInput.text = value;

            EditionDropdown.gameObject.SetActive(isInEditMode && selectedIndex < EditionDropdown.options.Count - 1);
            EditionTextInput.gameObject.SetActive(isInEditMode && selectedIndex >= EditionDropdown.options.Count - 1);
        }

        public void SetAsInteractable(bool isInteractable)
        {
            EditionDropdown.interactable = isInteractable;
            EditionTextInput.interactable = isInteractable;
        }
    }
}
