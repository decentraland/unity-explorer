using DCL.Audio;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DCL.UI
{
    public class SearchBarView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_InputField inputField;

        [field: SerializeField]
        public Button clearSearchButton;


        [FormerlySerializedAs("EnterTextAudioClipConfig")]
        [Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig InputTextAudio;

        [FormerlySerializedAs("ClearTextAudioClipConfig")]
        [field: SerializeField]
        public AudioClipConfig ClearTextAudio;

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
            AudioEventsBus.Instance.SendAudioEvent(ClearTextAudio);
        }

        private void OnValueChanged(string value)
        {
            AudioEventsBus.Instance.SendAudioEvent(InputTextAudio);
        }
    }
}
