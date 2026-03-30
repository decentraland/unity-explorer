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
        [field: SerializeField] public GameObject SpeakButtonContainer { get; private set; } = null!;
        [field: SerializeField] public Button SpeakButton { get; private set; } = null!;
        [field: SerializeField] public GameObject SpeakStateVisuals { get; private set; } = null!;
        [field: SerializeField] public GameObject SpeakingStateVisuals { get; private set; } = null!;
        [field: SerializeField] public GameObject HearText { get; private set; } = null!;

        [Header("Speak Button Color")]
        [SerializeField] private Image speakButtonImage = null!;
        [SerializeField] private Color speakingColor = new (0.075f, 0.82f, 0.125f, 1f);

        public void SetSpeaking(bool isSpeaking)
        {
            speakButtonImage.color = isSpeaking ? speakingColor : Color.white;
        }
    }
}
