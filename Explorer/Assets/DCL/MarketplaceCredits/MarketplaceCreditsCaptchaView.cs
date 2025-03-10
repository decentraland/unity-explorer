using DCL.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsCaptchaView : MonoBehaviour
    {
        [field: SerializeField]
        public SliderView MainSlider { get; private set; }

        [field: SerializeField]
        public RectTransform TargetArea { get; private set; }

        [field: SerializeField]
        public float MaxTargetAreaXPos { get; private set; }

        [field: SerializeField]
        public float MatchTargetOffset { get; private set; }

        private void Awake()
        {
            EventTrigger trigger = MainSlider.Slider.gameObject.AddComponent<EventTrigger>();
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            entry.callback.AddListener((eventData) => OnSliderPointerUp());
            trigger.triggers.Add(entry);
        }

        private void OnDestroy()
        {
            EventTrigger trigger = MainSlider.Slider.GetComponent<EventTrigger>();
            if (trigger != null)
                trigger.triggers.Clear();
        }

        public void SetCaptchaValue(float value) =>
            MainSlider.Slider.value = value;

        private void OnSliderPointerUp()
        {
            float targetAreaPositionValue = GetTargetAreaPositionValue();

            if (MainSlider.Slider.value >= targetAreaPositionValue - MatchTargetOffset && MainSlider.Slider.value <= targetAreaPositionValue + MatchTargetOffset)
                Debug.Log("SANTI LOG --> CAPTCHA SOLVED!");
            else
                Debug.Log("SANTI LOG --> CAPTCHA NOT SOLVED!");
        }

        private float GetTargetAreaPositionValue() =>
            TargetArea.anchoredPosition.x / MaxTargetAreaXPos;

        public void SetTargetAreaValue(float value)
        {
            TargetArea.position = new Vector3(
                Mathf.Clamp(value * MaxTargetAreaXPos, 0f, MaxTargetAreaXPos),
                TargetArea.position.y,
                TargetArea.position.z);
        }
    }
}
