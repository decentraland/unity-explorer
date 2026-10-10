using DCL.Audio;
using DCL.UI;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Fields
{
    public class MarketplaceCreditsCaptchaView : MonoBehaviour
    {
        public event Action<float> OnCaptchaSolved;

        [field: SerializeField]
        public GameObject ControlContainer { get; private set; }

        [field: SerializeField]
        public GameObject NotLoadedErrorContainer { get; private set; }

        [field: SerializeField]
        public GameObject NotSolvedErrorContainer { get; private set; }

        [field: SerializeField]
        public GameObject HandlersContainer { get; private set; }

        [field: SerializeField]
        public GameObject LoadingSpinner { get; private set; }

        [field: SerializeField]
        public SliderView MainSlider { get; private set; }

        [field: SerializeField]
        public Image TargetAreaBackgroundImage { get; private set; }

        [field: SerializeField]
        public Button ReloadFromNotLoadedStateButton { get; private set; }

        [field: SerializeField]
        public Button ReloadFromNotSolvedStateButton { get; private set; }

        [field: SerializeField]
        public AudioClipConfig CaptchaSolvedAudio { get; private set; }

        private float lastCaptchaValue;

        public void SetAsLoading(bool isLoading)
        {
            SetAsErrorState(false);
            HandlersContainer.SetActive(!isLoading);
            LoadingSpinner.SetActive(isLoading);
        }

        public void SetCaptchaPercentageValue(float percentageValue)
        {
            MainSlider.Slider.value = percentageValue / 100f;
            lastCaptchaValue = MainSlider.Slider.value;
        }

        public void SetTargetAreaImage(Sprite sprite)
        {
            TargetAreaBackgroundImage.sprite = sprite;
            TargetAreaBackgroundImage.enabled = true;
        }

        public void SetAsErrorState(bool isError, bool isNonSolvedError = true)
        {
            MainSlider.Slider.interactable = !isError;
            ControlContainer.SetActive(!isError);
            NotSolvedErrorContainer.gameObject.SetActive(isError && isNonSolvedError);
            NotLoadedErrorContainer.gameObject.SetActive(isError && !isNonSolvedError);
        }

        public void OnSliderPointerUp()
        {
            if (Mathf.Approximately(MainSlider.Slider.value, lastCaptchaValue))
                return;

            OnCaptchaSolved?.Invoke(MainSlider.Slider.value);
            lastCaptchaValue = MainSlider.Slider.value;
            UIAudioEventsBus.Instance.SendPlayAudioEvent(CaptchaSolvedAudio);
        }
    }
}
