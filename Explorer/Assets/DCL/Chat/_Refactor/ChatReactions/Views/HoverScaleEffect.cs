using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Reusable hover scale animation. Attach to any UI element.
    /// Scales the target transform to <see cref="hoveredScale"/> on pointer enter,
    /// back to Vector3.one on pointer exit. Kills active tweens on destroy.
    /// </summary>
    public class HoverScaleEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Transform target;
        [SerializeField] private float hoveredScale = 1.2f;
        [SerializeField] private float duration = 0.1f;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (target == null) return;
            target.DOKill();
            target.DOScale(Vector3.one * hoveredScale, duration).SetEase(Ease.OutQuad);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (target == null) return;
            target.DOKill();
            target.DOScale(Vector3.one, duration).SetEase(Ease.OutQuad);
        }

        private void OnDestroy()
        {
            if (target != null)
                target.DOKill();
        }

        /// <summary>Call when returning to a pool to kill in-flight tweens.</summary>
        public void ResetScale()
        {
            if (target == null) return;
            target.DOKill();
            target.localScale = Vector3.one;
        }
    }
}
