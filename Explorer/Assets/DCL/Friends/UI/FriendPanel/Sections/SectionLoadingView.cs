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

        public void Show()
        {
            CanvasGroup.alpha = 1;
            CanvasGroup.blocksRaycasts = true;
            LoadingBright.StartLoadingAnimation(null);
        }

        public void Hide()
        {
            CanvasGroup.DOFade(0, FadeDuration).OnComplete(() => CanvasGroup.blocksRaycasts = false);
            LoadingBright.FinishLoadingAnimation(null);
        }
    }
}
