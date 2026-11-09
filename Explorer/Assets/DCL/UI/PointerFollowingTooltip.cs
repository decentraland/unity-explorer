using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI
{
    /// <summary>
    ///     Shows a tooltip while the pointer is over this element and keeps it next to the cursor, the way the in-world hover banner does.
    /// </summary>
    public class PointerFollowingTooltip : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform tooltip = null!;
        [SerializeField] private Vector2 offset = new (100f, 0f);

        public void OnPointerEnter(PointerEventData eventData)
        {
            Follow(eventData);
            tooltip.gameObject.SetActive(true);
        }

        public void OnPointerMove(PointerEventData eventData) =>
            Follow(eventData);

        public void OnPointerExit(PointerEventData _) =>
            tooltip.gameObject.SetActive(false);

        private void OnDisable()
        {
            tooltip.gameObject.SetActive(false);
        }

        private void Follow(PointerEventData eventData)
        {
            var parent = (RectTransform)tooltip.parent;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.enterEventCamera, out Vector2 localPoint))
                tooltip.localPosition = localPoint + offset;
        }
    }
}
