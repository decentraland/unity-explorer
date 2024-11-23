using DCL.UI;
using DG.Tweening;
using UnityEngine;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class InfoSidePanelLoadingView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private LoadingBrightView loadingBright;
        [SerializeField] private float fadeDuration = 0.3f;

        public void Show()
        {
            canvasGroup.alpha = 1;
            canvasGroup.blocksRaycasts = true;
            loadingBright.StartLoadingAnimation(null);
        }

        public void Hide()
        {
            canvasGroup.DOFade(0, fadeDuration).OnComplete(() => canvasGroup.blocksRaycasts = false);
            loadingBright.FinishLoadingAnimation(null);
        }
    }
}
