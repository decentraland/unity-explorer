using SuperScrollView;
using System.Buffers;
using UnityEngine;

namespace DCL.UI.Utilities
{
    public static class LoopListExtensions
    {
        private const int MAX_ARRAY_LENGTH = 4;
        private static readonly ArrayPool<Vector3> CORNERS_POOL = ArrayPool<Vector3>.Create(MAX_ARRAY_LENGTH, 2);

        /// <summary>
        /// Checks if the last item in the LoopListView2 is visible in the viewport.
        /// </summary>
        /// <param name="loopListView">The LoopListView2 instance to check.</param>
        /// <returns>True if the last item is visible; otherwise, false.</returns>
        public static bool IsLastItemVisible(this LoopListView2 loopListView)
        {
            int total = loopListView.ItemTotalCount;
            if (total <= 0) return false;

            int targetIndex = loopListView is { ArrangeType: ListItemArrangeType.TopToBottom or ListItemArrangeType.LeftToRight } ? total - 1 : 0;
            var last = loopListView.GetShownItemByItemIndex(targetIndex);

            if (last == null)
                return false;

            // `last` is the last item in the buffer, which could still not be visible. We therefore need to check its actual visibility.
            RectTransform? itemRT = last.transform as RectTransform;
            if (itemRT == null) return false;

            RectTransform viewport = loopListView.ScrollRect.viewport;

            Vector3[] itemCorners = CORNERS_POOL.Rent(MAX_ARRAY_LENGTH);
            Vector3[] viewportCorners = CORNERS_POOL.Rent(MAX_ARRAY_LENGTH);
            itemRT.GetWorldCorners(itemCorners);
            viewport.GetWorldCorners(viewportCorners);

            bool isVisible = loopListView.ArrangeType switch
                             {
                                 ListItemArrangeType.TopToBottom => itemCorners[0].y <= viewportCorners[1].y && itemCorners[1].y >= viewportCorners[0].y,
                                 ListItemArrangeType.BottomToTop => itemCorners[1].y >= viewportCorners[0].y && itemCorners[0].y <= viewportCorners[1].y,
                                 ListItemArrangeType.LeftToRight => itemCorners[3].x <= viewportCorners[2].x && itemCorners[2].x >= viewportCorners[3].x,
                                 ListItemArrangeType.RightToLeft => itemCorners[2].x >= viewportCorners[3].x && itemCorners[3].x <= viewportCorners[2].x,
                                 _ => false
                             };

            CORNERS_POOL.Return(itemCorners);
            CORNERS_POOL.Return(viewportCorners);

            return isVisible;
        }
    }
}
