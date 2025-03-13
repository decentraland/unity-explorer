using DCL.UI;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public RectTransform TargetArea { get; private set; }

        [field: SerializeField]
        public float MaxTargetAreaXPos { get; private set; }

        [field: SerializeField]
        public Button ReloadFromNotLoadedStateButton { get; private set; }

        [field: SerializeField]
        public Button ReloadFromNotSolvedStateButton { get; private set; }

        private float lastCaptchaValue;

        private void Awake()
        {
            EventTrigger trigger = MainSlider.Slider.gameObject.AddComponent<EventTrigger>();
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            entry.callback.AddListener(_ => OnSliderPointerUp());
            trigger.triggers.Add(entry);
        }

        private void OnDestroy()
        {
            EventTrigger trigger = MainSlider.Slider.GetComponent<EventTrigger>();
            if (trigger != null)
                trigger.triggers.Clear();
        }

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

        public void SetTargetAreaPercentageValue(float percentageValue)
        {
            TargetArea.anchoredPosition = new Vector2(
                Mathf.Clamp(percentageValue / 100f * MaxTargetAreaXPos, 0f, MaxTargetAreaXPos),
                TargetArea.anchoredPosition.y);
        }

        public void SetAsErrorState(bool isError, bool isNotSolvedError = true)
        {
            ControlContainer.SetActive(!isError);
            NotSolvedErrorContainer.gameObject.SetActive(isError && isNotSolvedError);
            NotLoadedErrorContainer.gameObject.SetActive(isError && !isNotSolvedError);
        }

        private void OnSliderPointerUp()
        {
            if (Mathf.Approximately(MainSlider.Slider.value, lastCaptchaValue))
                return;

            //float targetAreaPositionValue = TargetArea.anchoredPosition.x / MaxTargetAreaXPos;
            //bool isCaptchaSolved = MainSlider.Slider.value >= targetAreaPositionValue - MatchTargetOffset && MainSlider.Slider.value <= targetAreaPositionValue + MatchTargetOffset;
            OnCaptchaSolved?.Invoke(MainSlider.Slider.value);
            lastCaptchaValue = MainSlider.Slider.value;
        }
    }
}
