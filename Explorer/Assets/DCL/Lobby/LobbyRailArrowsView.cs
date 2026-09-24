using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Lobby
{
    /// <summary>
    ///     Previous and Next buttons framing a paged rail. They fade in while the rail is hovered, and the button that has no page
    ///     left to go to is hidden.
    /// </summary>
    public class LobbyRailArrowsView : MonoBehaviour
    {
        private const float FADE_DURATION = 0.15f;

        [SerializeField] private CanvasGroup group = null!;

        private Tweener? fadeTween;

        [field: SerializeField]
        public Button Previous { get; private set; } = null!;

        [field: SerializeField]
        public Button Next { get; private set; } = null!;

        public void SetHovered(bool hovered)
        {
            fadeTween?.Kill();

            fadeTween = DOTween.To(() => group.alpha, alpha => group.alpha = alpha, hovered ? 1f : 0f, FADE_DURATION)
                               .SetEase(Ease.OutCubic)
                               .SetLink(gameObject);
        }

        public void SetPage(int page, int pageCount)
        {
            Previous.gameObject.SetActive(page > 0);
            Next.gameObject.SetActive(page < pageCount - 1);
        }
    }
}
