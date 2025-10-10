using SuperScrollView;
using System.Buffers;
using UnityEngine;

namespace DCL.UI.Utilities
{
    public static class LoopListExtensions
    {
        private const int MAX_ARRAY_LENGTH = 4;
        private static readonly ArrayPool<Vector3> EQUAL_TO_VERTICES = ArrayPool<Vector3>.Create(MAX_ARRAY_LENGTH, 2);

        public static bool IsLastItemVisible(this LoopListView2 loopListView)
        {

            int total = loopListView.ItemTotalCount;
            if (total <= 0) return false;

            int targetIndex = loopListView is { ArrangeType: ListItemArrangeType.TopToBottom or ListItemArrangeType.LeftToRight } ? total - 1 : 0;
            var last = loopListView.GetShownItemByItemIndex(targetIndex);

            if (last == null)
                return false;

            RectTransform? itemRT = last.transform as RectTransform;
            if (itemRT == null) return false;

            RectTransform viewport = loopListView.ScrollRect.viewport;

            Vector3[] itemCorners = EQUAL_TO_VERTICES.Rent(MAX_ARRAY_LENGTH);
            Vector3[] viewportCorners = EQUAL_TO_VERTICES.Rent(MAX_ARRAY_LENGTH);
            itemRT.GetWorldCorners(itemCorners);
            viewport.GetWorldCorners(viewportCorners);

            bool isVisible = false;
            switch (loopListView.ArrangeType)
            {
                case ListItemArrangeType.TopToBottom:
                    isVisible = itemCorners[0].y <= viewportCorners[1].y && itemCorners[1].y >= viewportCorners[0].y;
                    break;
                case ListItemArrangeType.BottomToTop:
                    isVisible = itemCorners[1].y >= viewportCorners[0].y && itemCorners[0].y <= viewportCorners[1].y;
                    break;
                case ListItemArrangeType.LeftToRight:
                    isVisible = itemCorners[3].x <= viewportCorners[2].x && itemCorners[2].x >= viewportCorners[3].x;
                    break;
                case ListItemArrangeType.RightToLeft:
                    isVisible = itemCorners[2].x >= viewportCorners[3].x && itemCorners[3].x <= viewportCorners[2].x;
                    break;
            }
            EQUAL_TO_VERTICES.Return(itemCorners);
            EQUAL_TO_VERTICES.Return(viewportCorners);

            return isVisible;
        }
    }
}
