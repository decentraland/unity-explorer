using UnityEngine;

namespace DCL.Chat.ChatReactions.Configs
{
    public readonly struct TooltipPositioningConfig
    {
        public readonly Vector2 Offset;
        public readonly float ArrowMinX;
        public readonly float ArrowMaxX;
        public readonly float ArrowXOffset;

        public TooltipPositioningConfig(Vector2 offset, float arrowMinX, float arrowMaxX, float arrowXOffset)
        {
            Offset = offset;
            ArrowMinX = arrowMinX;
            ArrowMaxX = arrowMaxX;
            ArrowXOffset = arrowXOffset;
        }
    }
}
