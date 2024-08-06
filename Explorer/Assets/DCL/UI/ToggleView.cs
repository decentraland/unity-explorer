using DCL.Audio;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ToggleView : MonoBehaviour
    {
        [field: SerializeField] internal bool autoToggleImagesOnToggle { get; private set; }

        [field: SerializeField]
        public Toggle Toggle { get; private set; }

        [field: SerializeField]
        public GameObject OnImage { get; private set; }

        [field: SerializeField]
        public GameObject OffImage { get; private set; }

        [field: SerializeField]
        public Image OnBackgroundImage { get; private set; }

        [field: SerializeField]
        public Image OffBackgroundImage { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ToggleOnAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig ToggleOffAudio { get; private set; }

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
            UIAudioEventsBus.Instance.SendPlayAudioEvent(toggle ? ToggleOnAudio : ToggleOffAudio);

            if (autoToggleImagesOnToggle)
            {
                OnImage.SetActive(toggle);
                OffImage.SetActive(!toggle);
                OnBackgroundImage.gameObject.SetActive(toggle);
                OffBackgroundImage.gameObject.SetActive(!toggle);
            }
        }
    }
}
