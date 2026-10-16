using UI.LayoutGroups;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class NavmapCategoriesStripView : MonoBehaviour
    {
        [SerializeField] private RectMask2D mask = null!;
        [SerializeField] private ScrollRect scrollRect = null!;
        [SerializeField] private RectTransform content = null!;
        [SerializeField] private HorizontalLayoutGroup rowLayout = null!;
        [SerializeField] private WrappingLayoutGroup wrapLayout = null!;
        [SerializeField] private ContentSizeFitter sizeFitter = null!;

        private Vector2 rowSize;
        private ContentSizeFitter.FitMode rowHorizontalFit;
        private ContentSizeFitter.FitMode rowVerticalFit;

        public bool IsWrapping { get; private set; }

        public void SetWrapping(bool wrap)
        {
            if (IsWrapping == wrap) return;

            IsWrapping = wrap;

            rowLayout.enabled = !wrap;
            wrapLayout.enabled = wrap;

            // Wrapped rows overflow the strip downwards instead of scrolling under its mask.
            scrollRect.enabled = !wrap;
            mask.enabled = !wrap;

            if (wrap)
            {
                rowSize = content.sizeDelta;
                rowHorizontalFit = sizeFitter.horizontalFit;
                rowVerticalFit = sizeFitter.verticalFit;

                sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                content.anchorMax = new Vector2(1f, content.anchorMax.y);
                content.sizeDelta = new Vector2(0f, rowSize.y);
            }
            else
            {
                sizeFitter.horizontalFit = rowHorizontalFit;
                sizeFitter.verticalFit = rowVerticalFit;
                content.anchorMax = new Vector2(content.anchorMin.x, content.anchorMax.y);
                content.sizeDelta = rowSize;
            }

            content.anchoredPosition = Vector2.zero;
        }
    }
}
