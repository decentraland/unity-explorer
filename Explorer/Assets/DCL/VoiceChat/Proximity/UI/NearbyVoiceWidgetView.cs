using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.Proximity
{
    public class NearbyVoiceWidgetView : ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] public Button CloseAreaButton { get; private set; } = null!;

        [field: Space]
        [field: SerializeField] public Toggle HearOthersToggle { get; private set; } = null!;
        [field: SerializeField] public GameObject VolumeSliderContainer { get; private set; } = null!;
        [field: SerializeField] public Slider VolumeSlider { get; private set; } = null!;
        [field: SerializeField] public Button SpeakButton { get; private set; } = null!;
        [field: SerializeField] public GameObject HearText { get; private set; } = null!;
    }
}
