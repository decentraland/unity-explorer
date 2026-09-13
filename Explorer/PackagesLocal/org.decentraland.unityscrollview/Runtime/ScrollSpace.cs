using UnityEngine;

namespace SuperScrollView
{
    /// <summary>
    /// Coordinate math shared by <see cref="LoopListView2"/> and
    /// <see cref="LoopGridView"/>.
    ///
    /// ---------------------------------------------------------------------
    /// INVARIANT: layout never rewrites a RectTransform's anchors or pivot.
    /// ---------------------------------------------------------------------
    /// Prefabs author items with whatever anchors suit them — equal anchors plus an
    /// explicit <c>sizeDelta</c> (a fixed 340x8 spacer), a corner pin, a stretch.
    /// Under Unity's rect model the same <c>sizeDelta</c> means a *size* when
    /// <c>anchorMin == anchorMax</c> on that axis and a *delta from the anchor span*
    /// when they differ, so forcing a stretch onto an item authored at
    /// <c>sizeDelta.x = 1000</c> silently widens it to <c>contentWidth + 1000</c>.
    /// The only thing layout is allowed to write is <c>anchoredPosition</c>, and only
    /// the component for the axis being arranged; the cross axis keeps whatever the
    /// prefab authored.
    ///
    /// Everything below is therefore expressed in Unity's own rect algebra:
    ///
    ///     anchorRefMin = parent.rect.min + anchorMin * parent.rect.size
    ///     anchorRefMax = parent.rect.min + anchorMax * parent.rect.size
    ///     rect.min     = anchorRefMin + anchoredPosition - sizeDelta * pivot
    ///     rect.max     = anchorRefMax + anchoredPosition + sizeDelta * (1 - pivot)
    ///
    /// which is exact for every anchor/pivot/sizeDelta combination (their difference
    /// is the anchor span plus sizeDelta, i.e. the rect's real size).
    ///
    /// ---------------------------------------------------------------------
    /// "Leading space" — one scroll coordinate for all four arrange directions.
    /// ---------------------------------------------------------------------
    /// Leading space measures, along the arrange axis, the distance from the content
    /// edge that index 0 sits against to the matching viewport edge, growing in the
    /// direction of increasing index. It is 0 when index 0 is flush with the viewport
    /// and <c>contentExtent - viewportExtent</c> at the far end, whichever way the
    /// list runs:
    ///
    ///     TopToBottom   leading = content.top    - viewport.top     (content moves up)
    ///     BottomToTop   leading = viewport.bottom - content.bottom  (content moves down)
    ///     LeftToRight   leading = viewport.left  - content.left     (content moves left)
    ///     RightToLeft   leading = content.right  - viewport.right   (content moves right)
    ///
    /// For the anchoring the prefabs actually use these collapse to the familiar
    /// forms — <c>+a.y</c>, <c>-a.y</c>, <c>-a.x</c>, <c>+a.x</c> — but the general
    /// form also holds for a Content that is stretch-anchored or pivoted anywhere,
    /// both of which occur in the shipped prefabs.
    /// </summary>
    internal static class ScrollSpace
    {
        internal static bool IsVertical(ListItemArrangeType arrange) =>
            arrange == ListItemArrangeType.TopToBottom || arrange == ListItemArrangeType.BottomToTop;

        /// <summary>Index of the arrange axis in a Vector2: 1 for vertical, 0 for horizontal.</summary>
        internal static int MajorAxis(ListItemArrangeType arrange) =>
            IsVertical(arrange) ? 1 : 0;

        /// <summary>
        /// How <c>anchoredPosition</c> along the arrange axis moves as leading space
        /// grows. Inverted for the two directions whose content slides towards the
        /// negative side of the axis.
        /// </summary>
        internal static float LeadingSign(ListItemArrangeType arrange) =>
            arrange == ListItemArrangeType.TopToBottom || arrange == ListItemArrangeType.RightToLeft ? 1f : -1f;

        /// <summary>The rect's four edges expressed in its parent's local space.</summary>
        internal static void EdgesInParent(RectTransform rt, Rect parent, out Vector2 min, out Vector2 max)
        {
            Vector2 anchorMin = rt.anchorMin;
            Vector2 anchorMax = rt.anchorMax;
            Vector2 pivot = rt.pivot;
            Vector2 sizeDelta = rt.sizeDelta;
            Vector2 anchored = rt.anchoredPosition;

            min = new Vector2(
                parent.xMin + (anchorMin.x * parent.width) + anchored.x - (sizeDelta.x * pivot.x),
                parent.yMin + (anchorMin.y * parent.height) + anchored.y - (sizeDelta.y * pivot.y));

            max = new Vector2(
                parent.xMin + (anchorMax.x * parent.width) + anchored.x + (sizeDelta.x * (1f - pivot.x)),
                parent.yMin + (anchorMax.y * parent.height) + anchored.y + (sizeDelta.y * (1f - pivot.y)));
        }

        /// <summary>Current scroll position of <paramref name="content"/> in leading space.</summary>
        internal static float LeadingOfContent(ListItemArrangeType arrange, RectTransform content, Rect viewport)
        {
            EdgesInParent(content, viewport, out Vector2 min, out Vector2 max);

            return arrange switch
                   {
                       ListItemArrangeType.TopToBottom => max.y - viewport.yMax,
                       ListItemArrangeType.BottomToTop => viewport.yMin - min.y,
                       ListItemArrangeType.LeftToRight => viewport.xMin - min.x,
                       ListItemArrangeType.RightToLeft => max.x - viewport.xMax,
                       _ => 0f,
                   };
        }

        /// <summary>
        /// Moves <paramref name="content"/> to <paramref name="leading"/>. Applied as a
        /// delta because leading space is affine in <c>anchoredPosition</c> with slope
        /// +/-1, which makes this exact without re-deriving the anchor terms.
        /// </summary>
        internal static void SetLeadingOfContent(ListItemArrangeType arrange, RectTransform content, Rect viewport, float leading)
        {
            float delta = (leading - LeadingOfContent(arrange, content, viewport)) * LeadingSign(arrange);

            if (delta == 0f)
                return;

            Vector2 anchored = content.anchoredPosition;

            if (IsVertical(arrange))
                anchored.y += delta;
            else
                anchored.x += delta;

            content.anchoredPosition = anchored;
        }

        /// <summary>
        /// The <c>anchoredPosition</c> component, on <paramref name="axis"/>, that puts
        /// the item's edge <paramref name="distance"/> px from the content's own edge on
        /// that axis. <paramref name="fromMax"/> selects which pair of edges: true
        /// measures the item's max edge down from the content's max edge (rightwards
        /// becomes leftwards, downwards from the top), false measures min edges up.
        /// </summary>
        internal static float AnchoredForEdge(RectTransform item, Rect content, int axis, float distance, bool fromMax)
        {
            float contentMin = axis == 0 ? content.xMin : content.yMin;
            float contentSize = axis == 0 ? content.width : content.height;
            float pivot = axis == 0 ? item.pivot.x : item.pivot.y;
            float sizeDelta = axis == 0 ? item.sizeDelta.x : item.sizeDelta.y;

            if (fromMax)
            {
                float anchorRefMax = contentMin + ((axis == 0 ? item.anchorMax.x : item.anchorMax.y) * contentSize);
                return contentMin + contentSize - distance - anchorRefMax - (sizeDelta * (1f - pivot));
            }

            float anchorRefMin = contentMin + ((axis == 0 ? item.anchorMin.x : item.anchorMin.y) * contentSize);
            return contentMin + distance - anchorRefMin + (sizeDelta * pivot);
        }

        /// <summary>
        /// Writes one component of <paramref name="item"/>'s anchored position, leaving
        /// the other exactly as the prefab authored it.
        /// </summary>
        internal static void SetAnchoredComponent(RectTransform item, int axis, float value)
        {
            Vector2 anchored = item.anchoredPosition;

            if (axis == 0)
            {
                if (anchored.x == value)
                    return;

                anchored.x = value;
            }
            else
            {
                if (anchored.y == value)
                    return;

                anchored.y = value;
            }

            item.anchoredPosition = anchored;
        }

        /// <summary>
        /// True when the rect's own extent on <paramref name="axis"/> is a function of its
        /// parent's (its anchors differ on that axis), which makes it useless as a
        /// measurement of the content the parent is being sized from.
        /// </summary>
        internal static bool StretchesOn(RectTransform rt, int axis) =>
            axis == 0
                ? !Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x)
                : !Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y);

        /// <summary>
        /// The <c>sizeDelta</c> component that gives <paramref name="size"/> as the real
        /// extent on <paramref name="axis"/>, accounting for the anchor span a
        /// stretch-anchored Content already contributes.
        /// </summary>
        internal static float SizeDeltaForExtent(RectTransform rt, Rect parent, int axis, float size)
        {
            float span = axis == 0
                ? (rt.anchorMax.x - rt.anchorMin.x) * parent.width
                : (rt.anchorMax.y - rt.anchorMin.y) * parent.height;

            return size - span;
        }

        /// <summary>
        /// The fraction of an extent, measured from the leading side of the arrange
        /// direction, that a snap pivot names. Pivots use uGUI's bottom-left-origin
        /// convention, so the arrange-axis component is inverted for the two directions
        /// that run from the max edge.
        /// </summary>
        internal static float SnapFactor(ListItemArrangeType arrange, Vector2 pivot) =>
            arrange switch
            {
                ListItemArrangeType.TopToBottom => 1f - pivot.y,
                ListItemArrangeType.BottomToTop => pivot.y,
                ListItemArrangeType.LeftToRight => pivot.x,
                ListItemArrangeType.RightToLeft => 1f - pivot.x,
                _ => 0f,
            };
    }
}
