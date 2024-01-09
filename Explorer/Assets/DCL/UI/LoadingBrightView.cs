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

        public void StartLoadingAnimation(GameObject loadingHide)
        {
            gameObject.SetActive(true);
            loadingHide.SetActive(false);
            loadingBrightObject.anchoredPosition = new Vector2(-referenceParent.rect.width, loadingBrightObject.anchoredPosition.y);
            loadingTween = loadingBrightObject.DOAnchorPosX(referenceParent.rect.width, 1f).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);
        }

        public void FinishLoadingAnimation(GameObject loadingHide)
        {
            gameObject.SetActive(false);
            loadingHide.SetActive(true);
            loadingTween.Kill();
        }
    }
}
