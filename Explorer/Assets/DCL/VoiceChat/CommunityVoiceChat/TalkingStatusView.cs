using DG.Tweening;
using TMPro;
using UnityEngine;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class TalkingStatusView : MonoBehaviour
    {
        private const float SHOW_HIDE_ANIMATION_DURATION = 0.5f;

        [SerializeField]
        public RectTransform NoPlayerTalking;

        [SerializeField]
        public RectTransform PeopleTalkingContainer;

        [SerializeField]
        public TMP_Text MultiplePeopleTalking;

        [SerializeField]
        public TMP_Text PlayerNameTalking;

        [SerializeField]
        public RectTransform isSpeakingIconRect;

        [SerializeField]
        public RectTransform isSpeakingIconOuterRect;

        private Sequence? isSpeakingCurrentSequence;

        public void SetSpeakingStatus(int speakingCount, string userName)
        {
            PeopleTalkingContainer.gameObject.SetActive(speakingCount >= 1);
            MultiplePeopleTalking.gameObject.SetActive(speakingCount > 1);
            PlayerNameTalking.gameObject.SetActive(speakingCount == 1);
            NoPlayerTalking.gameObject.SetActive(speakingCount == 0);
            PlayerNameTalking.text = userName;

            if (isSpeakingCurrentSequence != null)
            {
                isSpeakingCurrentSequence?.Kill();
                isSpeakingCurrentSequence = null;
            }

            if (speakingCount >= 1)
            {
                isSpeakingCurrentSequence = DOTween.Sequence();
                isSpeakingCurrentSequence.Append(isSpeakingIconRect.DOScaleY(0.2f, SHOW_HIDE_ANIMATION_DURATION));
                isSpeakingCurrentSequence.Join(isSpeakingIconOuterRect.DOScaleY(1, SHOW_HIDE_ANIMATION_DURATION));
                isSpeakingCurrentSequence.Append(isSpeakingIconOuterRect.DOScaleY(0.2f, SHOW_HIDE_ANIMATION_DURATION));
                isSpeakingCurrentSequence.Join(isSpeakingIconRect.DOScaleY(1, SHOW_HIDE_ANIMATION_DURATION));
                isSpeakingCurrentSequence.SetLoops(-1);
                isSpeakingCurrentSequence.Play();
            }
        }
    }
}
