using DCL.UI.ProfileElements;
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat
{
    public class VoiceChatIncomingCallView : MonoBehaviour
    {
        [field: SerializeField]
        public Button AcceptCallButton;

        [field: SerializeField]
        public Button RefuseCallButton;

        [field: SerializeField]
        public Image AnimatingBackground;

        [field: SerializeField]
        public SimpleProfileView ProfileView;

        private const float END_SCALE_ANIMATION_VALUE = 1.3f;
        private const float END_FADE_ANIMATION_VALUE = 0.8f;
        private const float ANIMATION_DURATION = 0.8f;

        private Sequence pulseAnimation;
        private Color initialColor;

        private void OnEnable()
        {
            CreatePulseAnimation();
        }

        private void OnDisable()
        {
            pulseAnimation?.Kill(true);
        }

        private void CreatePulseAnimation()
        {
            initialColor = AnimatingBackground.color;
            initialColor.a = 0;
            AnimatingBackground.transform.localScale = Vector3.zero;
            AnimatingBackground.color = initialColor;

            pulseAnimation = DOTween.Sequence();

            pulseAnimation.Append(AnimatingBackground.transform.DOScale(END_SCALE_ANIMATION_VALUE, ANIMATION_DURATION).SetEase(Ease.OutQuad));
            pulseAnimation.Join(AnimatingBackground.DOFade(END_FADE_ANIMATION_VALUE, ANIMATION_DURATION).SetEase(Ease.OutQuad));
            pulseAnimation.AppendInterval(0.2f);
            pulseAnimation.Append(AnimatingBackground.transform.DOScale(0f, ANIMATION_DURATION / 2).SetEase(Ease.InQuad));
            pulseAnimation.Join(AnimatingBackground.DOFade(0f, ANIMATION_DURATION / 2).SetEase(Ease.InQuad));
            pulseAnimation.SetLoops(-1, LoopType.Restart);
            pulseAnimation.Play();
        }
    }
}
