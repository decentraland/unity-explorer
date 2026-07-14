using DCL.UI;
using DG.Tweening;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public class SectionLoadingView : MonoBehaviour
    {
        [field: SerializeField] public CanvasGroup CanvasGroup { get; private set; }
        [field: SerializeField] public LoadingBrightView LoadingBright { get; private set; }
        [field: SerializeField] public float FadeDuration { get; private set; } = 0.3f;

        private Tweener? fadeTween;

        public void Show()
        {
            fadeTween?.Kill();
            CanvasGroup.alpha = 1;
            CanvasGroup.blocksRaycasts = true;
            LoadingBright.StartLoadingAnimation(null);
        }

        public void Hide()
        {
            fadeTween?.Kill();
            fadeTween = CanvasGroup.DOFade(0, FadeDuration).OnComplete(() => CanvasGroup.blocksRaycasts = false);
            LoadingBright.FinishLoadingAnimation(null);
        }

        private void OnDestroy()
        {
            // Without this the fade outlives panel teardown and DOTween keeps driving the destroyed CanvasGroup.
            fadeTween?.Kill();
            fadeTween = null;
        }
    }
}
