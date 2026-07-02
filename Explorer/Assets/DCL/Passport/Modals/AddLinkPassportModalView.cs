using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Modals
{
    public class AddLink_PassportModal : MonoBehaviour
    {
        public event Action<string, string> OnSave;

        [field: SerializeField]
        public Button BackgroundButton { get; private set; }

        [field: SerializeField]
        public TMP_InputField TitleInputField { get; private set; }

        [field: SerializeField]
        public TMP_InputField UrlInputField { get; private set; }

        [field: SerializeField]
        public Button CancelButton { get; private set; }

        [field: SerializeField]
        public Button SaveButton { get; private set; }

        private void Awake()
        {
            BackgroundButton.onClick.AddListener(Hide);
            CancelButton.onClick.AddListener(Hide);
            SaveButton.onClick.AddListener(Save);
            TitleInputField.onValueChanged.AddListener(_ => EnableOrDisableSaveButton());
            UrlInputField.onValueChanged.AddListener(_ => EnableOrDisableSaveButton());
        }

        private void OnDestroy()
        {
            BackgroundButton.onClick.RemoveAllListeners();
            CancelButton.onClick.RemoveAllListeners();
            SaveButton.onClick.RemoveAllListeners();
            TitleInputField.onValueChanged.RemoveAllListeners();
            UrlInputField.onValueChanged.RemoveAllListeners();
        }

        public void Show()
        {
            TitleInputField.text = string.Empty;
            UrlInputField.text = string.Empty;
            gameObject.SetActive(true);
            EnableOrDisableSaveButton();
        }

        private void Hide() =>
            gameObject.SetActive(false);

        private void Save()
        {
            OnSave?.Invoke(TitleInputField.text, UrlInputField.text);
            Hide();
        }

        private void EnableOrDisableSaveButton()
        {
            SaveButton.interactable = TitleInputField.text.Length > 0
                                      && UrlInputField.text.Length > 0
                                      && LinkValidator.IsValid(UrlInputField.text);
        }
    }
}
