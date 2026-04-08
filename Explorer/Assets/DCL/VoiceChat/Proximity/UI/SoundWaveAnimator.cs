using System;
using UnityEngine;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Drives three UI bars based on microphone amplitude.
    /// Each bar lerps at a different speed, creating a "wave" effect.
    /// Attack is fast, decay is slow — gives a natural voice-activity feel.
    /// </summary>
    public class SoundWaveAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform barLeft = null!;
        [SerializeField] private RectTransform barCenter = null!;
        [SerializeField] private RectTransform barRight = null!;

        [Header("Amplitude")]
        [Tooltip("Multiplier applied to raw mic RMS to normalize into 0..1 range")]
        [Range(0.5f, 50f)]
        [SerializeField] private float amplitudeSensitivity = 5f;

        [Tooltip("Normalized amplitude below this value is treated as silence")]
        [Range(0f, 0.1f)]
        [SerializeField] private float silenceThreshold = 0.01f;

        [Header("Smoothing")]
        [Tooltip("Lerp speed when amplitude is rising (fast attack)")]
        [Range(1f, 30f)]
        [SerializeField] private float smoothingUp = 14f;

        [Tooltip("Lerp speed when amplitude is falling (slow decay / tail)")]
        [Range(1f, 30f)]
        [SerializeField] private float smoothingDown = 4f;

        [Header("Bar Heights")]
        [SerializeField] private float barMinHeight = 1.8928f;
        [SerializeField] private float barMaxHeight = 9.0283f;

        [Header("Per-Bar Speed Multipliers")]
        [Tooltip("Left bar — slowest, creates lag")]
        [Range(0.1f, 5f)]
        [SerializeField] private float barSpeedLeft = 0.7f;

        [Tooltip("Center bar — fastest, leads the wave")]
        [Range(0.1f, 5f)]
        [SerializeField] private float barSpeedCenter = 1.0f;

        [Tooltip("Right bar — medium")]
        [Range(0.1f, 5f)]
        [SerializeField] private float barSpeedRight = 0.85f;

        private Func<float>? amplitudeProvider;
        private RectTransform[] bars = null!;
        private float[] smoothedHeights = null!;

        public void Initialize(Func<float> amplitudeGetter)
        {
            amplitudeProvider = amplitudeGetter;
            bars = new[] { barLeft, barCenter, barRight };
            smoothedHeights = new float[] { barMinHeight, barMinHeight, barMinHeight };
        }

        private void Update()
        {
            if (amplitudeProvider == null || bars.Length == 0)
                return;

            float raw = amplitudeProvider();
            float normalized = Mathf.Clamp01(raw * amplitudeSensitivity);

            if (normalized < silenceThreshold)
                normalized = 0f;

            float targetHeight = Mathf.Lerp(barMinHeight, barMaxHeight, normalized);

            ReadOnlySpan<float> barSpeeds = stackalloc float[]
            {
                barSpeedLeft,
                barSpeedCenter,
                barSpeedRight,
            };

            for (int i = 0; i < bars.Length; i++)
            {
                float globalSpeed = targetHeight > smoothedHeights[i]
                    ? smoothingUp
                    : smoothingDown;

                float speed = globalSpeed * barSpeeds[i] * Time.deltaTime;
                smoothedHeights[i] = Mathf.Lerp(smoothedHeights[i], targetHeight, speed);
                bars[i].sizeDelta = new Vector2(bars[i].sizeDelta.x, smoothedHeights[i]);
            }
        }

        private void OnDisable()
        {
            ResetToMin();
        }

        private void ResetToMin()
        {
            for (int i = 0; i < bars.Length; i++)
            {
                smoothedHeights[i] = barMinHeight;
                bars[i].sizeDelta = new Vector2(bars[i].sizeDelta.x, barMinHeight);
            }
        }
    }
}
