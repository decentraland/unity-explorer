using System;
using UnityEngine;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Replicates the nametag voice-chat wave animation for uGUI.
    /// Three bars ping-pong between two height sets while speaking,
    /// collapse to min height (dots) when silent.
    /// </summary>
    public class SoundWaveAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform barLeft = null!;
        [SerializeField] private RectTransform barCenter = null!;
        [SerializeField] private RectTransform barRight = null!;

        [Header("Heights")]
        [Min(0f)]
        [SerializeField] private float minHeight = 2f;
        [Min(0f)]
        [SerializeField] private float midHeight = 3.5f;
        [Min(0f)]
        [SerializeField] private float maxHeight = 10f;

        [Header("Animation")]
        [Tooltip("Transition duration in seconds (same as nametag 0.3s)")]
        [Min(0.01f)]
        [SerializeField] private float transitionDuration = 0.3f;

        [Header("Voice Detection")]
        [Tooltip("Raw RMS amplitude above this value is treated as speaking")]
        [Min(0f)]
        [SerializeField] private float speakingThreshold = 0.01f;

        private Func<float>? amplitudeProvider;
        private float transitionProgress;
        private bool altState;
        private bool isSpeaking;

        public void Initialize(Func<float> amplitudeGetter)
        {
            amplitudeProvider = amplitudeGetter;
        }

        private void Update()
        {
            if (amplitudeProvider == null) return;

            isSpeaking = amplitudeProvider() > speakingThreshold;

            // Advance transition progress
            transitionProgress += Time.deltaTime / transitionDuration;

            if (transitionProgress >= 1f)
            {
                transitionProgress = 0f;

                if (isSpeaking)
                    altState = !altState;
                else
                    altState = false;
            }

            float t = Mathf.SmoothStep(0f, 1f, transitionProgress);

            // Target heights depend on speaking + altState
            float leftTarget, centerTarget, rightTarget;

            if (!isSpeaking)
            {
                leftTarget = minHeight;
                centerTarget = minHeight;
                rightTarget = minHeight;
            }
            else if (!altState)
            {
                // State A: sides=mid, center=max (same as nametag default)
                leftTarget = midHeight;
                centerTarget = maxHeight;
                rightTarget = midHeight;
            }
            else
            {
                // State B: sides=max, center=mid (same as nametag --alt)
                leftTarget = maxHeight;
                centerTarget = midHeight;
                rightTarget = maxHeight;
            }

            // Lerp from current to target
            SetBarHeight(barLeft, Mathf.Lerp(barLeft.sizeDelta.y, leftTarget, t));
            SetBarHeight(barCenter, Mathf.Lerp(barCenter.sizeDelta.y, centerTarget, t));
            SetBarHeight(barRight, Mathf.Lerp(barRight.sizeDelta.y, rightTarget, t));
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
            transitionProgress = 0f;
            altState = false;
        }
    }
}
