using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Lobby
{
    /// <summary>
    ///     Compact place card of the lobby: thumbnail with the online users on top, title and creator.
    ///     Hovering raises the footer over the thumbnail, swapping the creator for the Jump in button.
    /// </summary>
    public class LobbyPlaceCardView : LobbyCardView, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HOVER_DURATION = 0.3f;

        [field: SerializeField]
        public TMP_Text CreatorText { get; private set; } = null!;

        [field: SerializeField]
        public GameObject OnlineCounter { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text OnlineCountText { get; private set; } = null!;

        [SerializeField] private RectTransform header = null!;
        [SerializeField] private RectTransform footer = null!;
        [SerializeField] private CanvasGroup creatorGroup = null!;
        [SerializeField] private CanvasGroup jumpInGroup = null!;

        [Tooltip("How much the footer grows over the thumbnail while hovered")]
        [SerializeField] private float hoverFooterGrowth = 24f;

        private Vector2 headerSize;
        private Vector2 footerSize;
        private Sequence? hoverTween;

        public void OnPointerEnter(PointerEventData _) =>
            SetHovered(true, instant: false);

        public void OnPointerExit(PointerEventData _) =>
            SetHovered(false, instant: false);

        private void Awake()
        {
            headerSize = header.sizeDelta;
            footerSize = footer.sizeDelta;
        }

        // Cards are reused across shows, so a card hidden while hovered must not come back raised
        private void OnEnable()
        {
            SetHovered(false, instant: true);
        }

        private void OnDisable()
        {
            hoverTween?.Kill();
        }

        private void SetHovered(bool hovered, bool instant)
        {
            hoverTween?.Kill();

            float growth = hovered ? hoverFooterGrowth : 0f;
            var headerTarget = new Vector2(headerSize.x, headerSize.y - growth);
            var footerTarget = new Vector2(footerSize.x, footerSize.y + growth);
            float creatorAlpha = hovered ? 0f : 1f;
            float jumpInAlpha = hovered ? 1f : 0f;

            // An invisible button must never take the click meant for the card itself
            jumpInGroup.interactable = hovered;
            jumpInGroup.blocksRaycasts = hovered;

            if (instant)
            {
                header.sizeDelta = headerTarget;
                footer.sizeDelta = footerTarget;
                creatorGroup.alpha = creatorAlpha;
                jumpInGroup.alpha = jumpInAlpha;
                return;
            }

            hoverTween = DOTween.Sequence()
                                .Join(DOTween.To(() => header.sizeDelta, size => header.sizeDelta = size, headerTarget, HOVER_DURATION))
                                .Join(DOTween.To(() => footer.sizeDelta, size => footer.sizeDelta = size, footerTarget, HOVER_DURATION))
                                .Join(DOTween.To(() => creatorGroup.alpha, alpha => creatorGroup.alpha = alpha, creatorAlpha, HOVER_DURATION))
                                .Join(DOTween.To(() => jumpInGroup.alpha, alpha => jumpInGroup.alpha = alpha, jumpInAlpha, HOVER_DURATION))
                                .SetEase(Ease.OutQuad)
                                .SetLink(gameObject);
        }
    }
}
