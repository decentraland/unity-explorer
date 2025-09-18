using DCL.Audio;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardVoiceChatView : MonoBehaviour
    {
        private const float ANIMATION_DURATION = 0.5f;

        [field: SerializeField]
        public GameObject VoiceChatPanel;

        [field: SerializeField]
        public GameObject ModeratorControlPanel;

        [field: SerializeField]
        public GameObject LiveStreamPanel;

        [field: SerializeField]
        public Button StartStreamButton;

        [field: SerializeField]
        public Button JoinStreamButton;

        [field: SerializeField]
        public Button ListeningButton;

        [field: SerializeField]
        public TMP_Text ListenersCount;

        [field: SerializeField]
        internal RectTransform isSpeakingIconRect { get; private set; }

        [field: SerializeField]
        internal RectTransform isSpeakingIconOuterRect { get; private set; }

        [field: SerializeField]
        public AudioClipConfig StartStreamAudio { get; private set; }

        private Sequence? isSpeakingCurrentSequence;

        public void HandleListeningAnimation(bool isAnimationEnabled)
        {
            isSpeakingCurrentSequence?.Kill();
            isSpeakingCurrentSequence = null;

            if (isAnimationEnabled)
            {
                isSpeakingCurrentSequence = DOTween.Sequence();
                isSpeakingCurrentSequence.Append(isSpeakingIconRect.DOScaleY(0.2f, ANIMATION_DURATION));
                isSpeakingCurrentSequence.Join(isSpeakingIconOuterRect.DOScaleY(1, ANIMATION_DURATION));
                isSpeakingCurrentSequence.Append(isSpeakingIconOuterRect.DOScaleY(0.2f, ANIMATION_DURATION));
                isSpeakingCurrentSequence.Join(isSpeakingIconRect.DOScaleY(1, ANIMATION_DURATION));
                isSpeakingCurrentSequence.SetLoops(-1);
                isSpeakingCurrentSequence.Play();
            }
        }
    }
}
