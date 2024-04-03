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


        [Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig EnterTextAudioClipConfig;

        [field: SerializeField]
        public AudioClipConfig ClearTextAudioClipConfig;

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
            UIAudioEventsBus.Instance.SendAudioEvent(ClearTextAudioClipConfig);
        }

        private void OnValueChanged(string value)
        {
            UIAudioEventsBus.Instance.SendAudioEvent(EnterTextAudioClipConfig);
        }
    }
}
