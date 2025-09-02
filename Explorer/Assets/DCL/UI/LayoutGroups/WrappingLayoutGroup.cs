using UnityEngine;
using UnityEngine.UI;

namespace UI.LayoutGroups
{
    [AddComponentMenu("Layout/Wrapping Layout Group")]
    public class WrappingLayoutGroup : LayoutGroup
    {
        [field: SerializeField] public float SpacingX {get; set;} = 10f;
        [field: SerializeField] public float SpacingY {get; set;} = 10f;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            CalcLayout();
        }

        public override void CalculateLayoutInputVertical()
        {
            CalcLayout();
        }

        public override void SetLayoutHorizontal()
        {
            CalcLayout();
        }

        public override void SetLayoutVertical()
        {
            CalcLayout();
        }

        private void CalcLayout()
        {
            float parentWidth = rectTransform.rect.width;

            float x = padding.left;
            float y = padding.top;
            float rowHeight = 0;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                RectTransform child = rectChildren[i];

                float childWidth = LayoutUtility.GetPreferredSize(child, 0);
                float childHeight = LayoutUtility.GetPreferredSize(child, 1);

                if (x + childWidth > parentWidth - padding.right && x > padding.left)
                {
                    // New row
                    x = padding.left;
                    y += rowHeight + SpacingX;
                    rowHeight = 0;
                }

                SetChildAlongAxis(child, 0, x, childWidth);
                SetChildAlongAxis(child, 1, y, childHeight);

                x += childWidth + SpacingY;
                rowHeight = Mathf.Max(rowHeight, childHeight);
            }

            float totalHeight = y + rowHeight + padding.bottom;

            SetLayoutInputForAxis(parentWidth, parentWidth, -1, 0);
            SetLayoutInputForAxis(totalHeight, totalHeight, -1, 1);
        }
    }
}
