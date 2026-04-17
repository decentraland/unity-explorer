using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.UI
{
    public class NearbyVoiceWidgetView : ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] public Button CloseAreaButton { get; private set; } = null!;

        [field: Space]
        [field: SerializeField] public Toggle HearOthersToggle { get; private set; } = null!;
        [field: SerializeField] public GameObject VolumeSliderContainer { get; private set; } = null!;
        [field: SerializeField] public Slider VolumeSlider { get; private set; } = null!;
        [field: SerializeField] public TMP_Text HearText { get; private set; } = null!;

        [field: Header("SPEAK BUTTON")]
        [field: SerializeField] public GameObject SpeakButtonContainer { get; private set; } = null!;
        [field: SerializeField] public Button SpeakButton { get; private set; } = null!;
        [field: SerializeField] public GameObject SpeakStateVisuals { get; private set; } = null!;
        [field: SerializeField] public GameObject SpeakingStateVisuals { get; private set; } = null!;

        [SerializeField] private Image speakButtonImage = null!;
        [SerializeField] private Color speakingColor = new (0.075f, 0.82f, 0.125f, 1f);
        [SerializeField] private Color speakingColorBright = new (0.15f, 1f, 0.25f, 1f);

        [Min(0.5f)][Tooltip("Multiplier for raw RMS amplitude")]
        [SerializeField] private float colorSensitivity = 8f;

        [Min(1f)][Tooltip("How fast color reacts to amplitude changes")]
        [SerializeField] private float colorSmoothing = 12f;

        private Func<float> amplitudeProvider;
        private bool isSpeakingState;
        private float smoothedIntensity;

        private void Awake()
        {
            enabled = false;
        }

        public void Initialize(Func<float> provider)
        {
            amplitudeProvider = provider;
            enabled = true;
        }

        public void SetSpeaking(bool isSpeaking)
        {
            isSpeakingState = isSpeaking;

            if (!isSpeaking)
            {
                smoothedIntensity = 0f;
                speakButtonImage.color = Color.white;
            }
        }

        private void Update()
        {
            if (isSpeakingState)
            {
                float target = Mathf.Clamp01(amplitudeProvider() * colorSensitivity);
                smoothedIntensity = Mathf.Lerp(smoothedIntensity, target, colorSmoothing * Time.deltaTime);
                speakButtonImage.color = Color.Lerp(speakingColor, speakingColorBright, smoothedIntensity);
            }
        }
    }
}
