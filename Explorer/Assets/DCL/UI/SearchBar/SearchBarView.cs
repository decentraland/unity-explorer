using DCL.Audio;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class SearchBarView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_InputField inputField;

        [field: SerializeField]
        public Button clearSearchButton;

        [field: SerializeField]
        public UIAudioType EnterTextAudioType = UIAudioType.GENERIC_INPUT_TEXT;

        [field: SerializeField]
        public UIAudioType ClearTextAudioType = UIAudioType.GENERIC_INPUT_CLEAR_TEXT;

        private void Awake()
        {
            inputField.onValueChanged.AddListener(OnValueChanged);
            clearSearchButton.onClick.AddListener(OnClearText);
        }

        private void OnDestroy()
        {
            inputField.onValueChanged.RemoveListener(OnValueChanged);
            clearSearchButton.onClick.RemoveListener(OnClearText);
        }

        private void OnClearText()
        {
            UIAudioEventsBus.Instance.SendAudioEvent(ClearTextAudioType);
        }

        private void OnValueChanged(string value)
        {
            UIAudioEventsBus.Instance.SendAudioEvent(EnterTextAudioType);
        }
    }
}
