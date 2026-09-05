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

        [field: SerializeField]
        public Button DecreaseButton { get; private set; }

        [field: SerializeField]
        public Button IncreaseButton { get; private set; }
        
        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig SliderValueChanged { get; private set; }

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
