using DCL.Audio;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ToggleView : MonoBehaviour
    {
        [field: SerializeField]
        public Toggle Toggle { get; private set; }

        [field: SerializeField]
        public GameObject OnImage { get; private set; }

        [field: SerializeField]
        public GameObject OffImage { get; private set; }

        [Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ToggleOnAudio;
        [field: SerializeField]
        public AudioClipConfig ToggleOffAudio;

        private void OnEnable()
        {
            Toggle.onValueChanged.AddListener(OnToggle);
        }

        private void OnDisable()
        {
            Toggle.onValueChanged.RemoveListener(OnToggle);
        }

        private void OnToggle(bool toggle)
        {
            AudioEventsBus.Instance.SendPlayAudioEvent(toggle ? ToggleOnAudio : ToggleOffAudio);
        }
    }
}
