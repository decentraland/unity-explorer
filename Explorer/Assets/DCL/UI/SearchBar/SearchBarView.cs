using DCL.Audio;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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


        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig InputTextAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig ClearTextAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig SubmitAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig SelectAudio { get; private set; }

        private void OnEnable()
        {
            inputField.onValueChanged.AddListener(OnValueChanged);
            inputField.onSubmit.AddListener(OnSubmit);
            inputField.onSelect.AddListener(OnSelect);
            clearSearchButton.onClick.AddListener(OnClearText);
        }

        private void OnDisable()
        {
            inputField.onValueChanged.RemoveListener(OnValueChanged);
            clearSearchButton.onClick.RemoveListener(OnClearText);
            inputField.onSubmit.RemoveListener(OnSubmit);
        }

        private void OnSelect(string text)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(SelectAudio);
        }

        private void OnSubmit(string text)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(SubmitAudio);
        }

        private void OnClearText()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ClearTextAudio);
        }

        private void OnValueChanged(string value)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(InputTextAudio);
        }
    }
}
