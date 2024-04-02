using DCL.Audio;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ToggleView : MonoBehaviour
    {
        [Header("Audio")]
        [field: SerializeField]
        public UIAudioType OnAudioType = UIAudioType.GENERIC_TOGGLE_ON;
        [field: SerializeField]
        public UIAudioType OffAudioType = UIAudioType.GENERIC_TOGGLE_OFF;
        [field: SerializeField]
        public Toggle Toggle { get; private set; }

        [field: SerializeField]
        public GameObject OnImage { get; private set; }

        [field: SerializeField]
        public GameObject OffImage { get; private set; }

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
            UIAudioEventsBus.Instance.SendAudioEvent(toggle ? OnAudioType : OffAudioType);
        }
    }
}
