using System;
using UnityEngine;

namespace DCL.VoiceChat.Nearby
{
    public class SoundWaveAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform barLeft = null!;
        [SerializeField] private RectTransform barCenter = null!;
        [SerializeField] private RectTransform barRight = null!;

        [Header("Heights")]
        [Min(0f)]
        [SerializeField] private float minHeight = 2f;
        [Min(0f)]
        [SerializeField] private float midHeight = 4f;
        [Min(0f)]
        [SerializeField] private float maxHeight = 10f;

        [Header("Wave")]
        [Min(0.01f)]
        [SerializeField] private float waveDuration = 0.3f;

        [Header("Attack / Decay")]
        [Min(0.01f)]
        [SerializeField] private float attackDuration = 0.15f;
        [Min(0.01f)]
        [SerializeField] private float decayDuration = 0.3f;

        [Header("Voice Detection")]
        [Min(0f)]
        [SerializeField] private float speakingThreshold = 0.001f;

        private Func<float>? amplitudeProvider;
        private float waveProgress;
        private float speakingBlend;
        private bool altState;

        public void Initialize(Func<float> amplitudeGetter)
        {
            amplitudeProvider = amplitudeGetter;
        }

        private void Update()
        {
            if (amplitudeProvider == null) return;

            bool isSpeaking = amplitudeProvider() > speakingThreshold;

            float blendSpeed = isSpeaking ? 1f / attackDuration : 1f / decayDuration;
            speakingBlend = Mathf.MoveTowards(speakingBlend, isSpeaking ? 1f : 0f, blendSpeed * Time.deltaTime);

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

            float w = Mathf.SmoothStep(0f, 1f, waveProgress);

            float leftActive = Mathf.Lerp(altState ? maxHeight : midHeight, altState ? midHeight : maxHeight, w);
            float centerActive = Mathf.Lerp(altState ? midHeight : maxHeight, altState ? maxHeight : midHeight, w);

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
