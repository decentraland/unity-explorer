using System;
using UnityEngine;

namespace DCL.VoiceChat.UI
{
    /// <summary>
    /// Replicates the nametag voice-chat wave animation for uGUI.
    /// Three bars ping-pong between two height sets while speaking,
    /// collapse to min height (dots) when silent.
    /// Wave oscillation keeps running during attack/decay for organic feel.
    /// </summary>
    public class SoundWaveAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform barLeft = null!;
        [SerializeField] private RectTransform barCenter = null!;
        [SerializeField] private RectTransform barRight = null!;

        [Header("HEIGHTS")]
        [Min(0f)]
        [SerializeField] private float minHeight = 2f;
        [Min(0f)]
        [SerializeField] private float midHeight = 4f;
        [Min(0f)]
        [SerializeField] private float maxHeight = 10f;

        [Header("WAVE")]
        [Tooltip("Ping-pong cycle duration while speaking")]
        [Min(0.01f)]
        [SerializeField] private float waveDuration = 0.3f;

        [Header("ATTACK / DECAY")]
        [Tooltip("Time to grow from dots to speaking position")]
        [Min(0.01f)]
        [SerializeField] private float attackDuration = 0.15f;

        [Tooltip("Time to shrink back to dots")]
        [Min(0.01f)]
        [SerializeField] private float decayDuration = 0.3f;

        [Header("VOICE DETECTION")]
        [Tooltip("Raw RMS amplitude above this value is treated as speaking")]
        [Min(0f)]
        [SerializeField] private float speakingThreshold = 0.001f;

        private Func<float> amplitudeProvider;

        private float waveProgress;
        private float speakingBlend; // 0 = dots, 1 = fully speaking
        private bool altState;

        private void Awake()
        {
            enabled = false;
        }

        public void Initialize(Func<float> amplitudeGetter)
        {
            amplitudeProvider = amplitudeGetter;
            enabled = true;
        }

        private void Update()
        {
            bool isSpeaking = amplitudeProvider() > speakingThreshold;

            // Attack / Decay
            float blendSpeed = isSpeaking ? 1f / attackDuration : 1f / decayDuration;
            speakingBlend = Mathf.MoveTowards(speakingBlend, isSpeaking ? 1f : 0f, blendSpeed * Time.deltaTime);

            // Wave keeps running as long as bars are visible — no jerk on decay
            if (speakingBlend > 0f)
            {
                waveProgress += Time.deltaTime / waveDuration;

                if (waveProgress >= 1f)
                {
                    waveProgress = 0f;
                    altState = !altState;
                }
            }
            else
            {
                waveProgress = 0f;
                altState = false;
            }

            // Lerp between current wave state and next
            float w = Mathf.SmoothStep(0f, 1f, waveProgress);

            float leftActive = Mathf.Lerp(altState ? maxHeight : midHeight, altState ? midHeight : maxHeight, w);
            float centerActive = Mathf.Lerp(altState ? midHeight : maxHeight, altState ? maxHeight : midHeight, w);

            // Final: blend between dots (min) and wave heights
            SetBarHeight(barLeft, Mathf.Lerp(minHeight, leftActive, speakingBlend));
            SetBarHeight(barCenter, Mathf.Lerp(minHeight, centerActive, speakingBlend));
            SetBarHeight(barRight, Mathf.Lerp(minHeight, leftActive, speakingBlend));
        }

        private static void SetBarHeight(RectTransform bar, float height)
        {
            bar.sizeDelta = new Vector2(bar.sizeDelta.x, height);
        }

        private void OnDisable()
        {
            SetBarHeight(barLeft, minHeight);
            SetBarHeight(barCenter, minHeight);
            SetBarHeight(barRight, minHeight);
            waveProgress = 0f;
            speakingBlend = 0f;
            altState = false;
        }
    }
}
