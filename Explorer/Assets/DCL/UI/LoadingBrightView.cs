using DCL.Audio;
using DG.Tweening;
using UnityEngine;

namespace DCL.UI
{
    public class LoadingBrightView : MonoBehaviour
    {
        [field: SerializeField]
        private RectTransform referenceParent { get; set; }

        [field: SerializeField]
        private RectTransform loadingBrightObject { get; set; }

        private Tween loadingTween;

        [Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig LoadingStartedAudio;
        [field: SerializeField]
        public AudioClipConfig LoadingFinishedAudio;


        public void StartLoadingAnimation(GameObject loadingHide)
        {
            loadingTween.Kill();
            gameObject.SetActive(true);
            loadingHide.SetActive(false);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(LoadingStartedAudio);
            loadingBrightObject.anchoredPosition = new Vector2(-referenceParent.rect.width, loadingBrightObject.anchoredPosition.y);
            loadingTween = loadingBrightObject.DOAnchorPosX(referenceParent.rect.width, 1f).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);
        }

        public void FinishLoadingAnimation(GameObject loadingHide)
        {
            gameObject.SetActive(false);
            loadingHide.SetActive(true);
            loadingTween.Kill();
            UIAudioEventsBus.Instance.SendPlayAudioEvent(LoadingFinishedAudio);
        }
    }
}
