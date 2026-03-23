using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatMessages
{
    /// <summary>
    /// Positions the reaction tooltip body (horizontally centered) and its arrow
    /// (pointing at the hovered pill). Mirrors <see cref="DCL.Chat.ChatReactions.ReactionPanelPositioner"/>
    /// for the shortcuts bar.
    /// </summary>
    public sealed class ReactionTooltipPositioner
    {
        private readonly RectTransform tooltipRect;
        private readonly RectTransform? arrowTransform;
        private readonly RectTransform? centeringReference;
        private readonly Vector2 offset;
        private readonly float arrowMinX;
        private readonly float arrowMaxX;
        private readonly float arrowXOffset;
        private readonly Vector3[] corners = new Vector3[4];

        public ReactionTooltipPositioner(
            RectTransform tooltipRect,
            RectTransform? arrowTransform,
            RectTransform? centeringReference,
            TooltipPositioningConfig config)
        {
            this.tooltipRect = tooltipRect;
            this.arrowTransform = arrowTransform;
            this.centeringReference = centeringReference;
            offset = config.Offset;
            arrowMinX = config.ArrowMinX;
            arrowMaxX = config.ArrowMaxX;
            arrowXOffset = config.ArrowXOffset;
        }

        public void PositionAbovePill(RectTransform pillTransform)
        {
            Vector3 pillTopCenter = GetPillTopCenter(pillTransform);
            PositionTooltipCentered(pillTopCenter);
            PositionArrowAtPill(pillTransform);
        }

        private void PositionTooltipCentered(Vector3 pillTopCenter)
        {
            var tooltipParent = (RectTransform)tooltipRect.parent;
            Vector3 localPos = tooltipParent.InverseTransformPoint(pillTopCenter);
            float centerX = GetTooltipCenterX(tooltipParent);

            tooltipRect.localPosition = new Vector3(
                centerX + offset.x,
                localPos.y + offset.y,
                0f);
        }

        private float GetTooltipCenterX(RectTransform tooltipParent)
        {
            if (centeringReference == null)
                return tooltipParent.rect.center.x;

            Vector3 refCenter = centeringReference.TransformPoint(centeringReference.rect.center);
            return tooltipParent.InverseTransformPoint(refCenter).x;
        }

        private void PositionArrowAtPill(RectTransform pillTransform)
        {
            if (arrowTransform == null) return;

            // pill.position gives the pivot world X. Since reaction pills have a centered
            // pivot (0.5), this matches the visual center without needing GetWorldCorners.
            Vector3 pillCenter = pillTransform.position;
            var arrowParent = (RectTransform)arrowTransform.parent;
            float localX = arrowParent.InverseTransformPoint(pillCenter).x + arrowXOffset;
            float clampedX = Mathf.Clamp(localX, arrowMinX, arrowMaxX);

            arrowTransform.anchoredPosition = new Vector2(
                clampedX,
                arrowTransform.anchoredPosition.y);
        }

        private Vector3 GetPillTopCenter(RectTransform pill)
        {
            pill.GetWorldCorners(corners);
            // corners: 0=bottom-left, 1=top-left, 2=top-right, 3=bottom-right
            return new Vector3(
                (corners[1].x + corners[2].x) * 0.5f,
                corners[1].y,
                corners[1].z);
        }
    }
}
