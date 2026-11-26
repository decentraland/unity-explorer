using Cysharp.Threading.Tasks;
using DG.Tweening;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.Cart.UI
{
    public class ShopCartModalView : ViewBase, IView
    {
        private const float POPUP_SHOW_ANIMATION_TIME = 0.3f;
        private const float POPUP_HIDE_ANIMATION_TIME = 0.2f;

        [field: SerializeField] public RectTransform ContainerTransform { get; private set; } = null!;
        [field: SerializeField] public TMP_Text TitleText { get; private set; } = null!;

        [field: Header("Lines")]
        [field: SerializeField] public Transform LinesContainer { get; private set; } = null!;
        [field: SerializeField] public ShopCartLineView LinePrefab { get; private set; } = null!;
        [field: SerializeField] public GameObject EmptyState { get; private set; } = null!;
        [field: SerializeField] public TMP_Text TotalCreditsText { get; private set; } = null!;

        [field: Header("Actions")]
        [field: SerializeField] public Button CheckoutButton { get; private set; } = null!;
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
        [field: SerializeField] public Button CloseBackground { get; private set; } = null!;

        private Vector3 worldAnchor;
        private float anchorGap;

        /// <summary>
        ///     Sets the world-space point the container's top-right corner hangs <paramref name="gap" /> under. It is
        ///     applied when the view is shown.
        /// </summary>
        public void SetAnchor(Vector3 worldAnchor, float gap)
        {
            this.worldAnchor = worldAnchor;
            anchorGap = gap;
        }

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            // Placed here and not before the view is activated: an inactive canvas keeps the size and scale of the
            // resolution it was last shown at, which turns the anchor into the wrong local position.
            PositionBelowAnchor();

            ContainerTransform.DOKill();
            ContainerTransform.localScale = Vector3.zero;
            return ContainerTransform.DOScale(Vector3.one, POPUP_SHOW_ANIMATION_TIME).SetEase(Ease.OutBack).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            ContainerTransform.DOKill();
            return ContainerTransform.DOScale(Vector3.zero, POPUP_HIDE_ANIMATION_TIME).SetEase(Ease.InBack).ToUniTask(cancellationToken: ct);
        }

        // Kept inside the parent rect so the popup never leaves the screen.
        private void PositionBelowAnchor()
        {
            var parent = (RectTransform)ContainerTransform.parent;
            Vector3 anchor = parent.InverseTransformPoint(worldAnchor);
            Rect rect = ContainerTransform.rect;
            Rect bounds = parent.rect;

            // rect.xMax/yMax are the container's top-right edges relative to its pivot, so subtracting them turns the
            // wanted corner position into a pivot position
            float x = Mathf.Clamp(anchor.x - rect.xMax, bounds.xMin - rect.xMin, bounds.xMax - rect.xMax);
            float y = Mathf.Clamp(anchor.y - anchorGap - rect.yMax, bounds.yMin - rect.yMin, bounds.yMax - rect.yMax);
            ContainerTransform.localPosition = new Vector3(x, y, 0f);
        }
    }
}
