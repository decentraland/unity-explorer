using DCL.Audio;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class SliderView : MonoBehaviour
    {
        [field: SerializeField]
        public Slider Slider { get; private set; }

        [Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig SliderValueChanged;

        private void OnEnable()
        {
            Slider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        private void OnDisable()
        {
            Slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }

        private void OnSliderValueChanged(float value)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(SliderValueChanged);
        }
    }
}
