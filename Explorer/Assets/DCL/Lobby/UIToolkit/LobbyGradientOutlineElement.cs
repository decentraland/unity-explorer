using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     A rounded ring the width of its box, filled with a vertical gradient from one colour at the top to another at
    ///     the bottom, and clear inside: the hover outline of the hero and photo cards. The stylesheet places and sizes
    ///     it, the box is the outer edge of the ring and nothing is drawn inside it, so a gap can show between the ring
    ///     and the card it frames.
    /// </summary>
    [UxmlElement]
    public partial class LobbyGradientOutlineElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-gradient-outline";

        /// <summary>Straight runs a quarter circle is drawn with.</summary>
        private const int CORNER_SEGMENTS = 8;

        private static readonly Color DEFAULT_TOP_COLOR = new (1f, 45f / 255f, 85f / 255f);
        private static readonly Color DEFAULT_BOTTOM_COLOR = new (198f / 255f, 64f / 255f, 205f / 255f);

        private float thickness = 2f;
        private float radius;
        private Color topColor = DEFAULT_TOP_COLOR;
        private Color bottomColor = DEFAULT_BOTTOM_COLOR;

        [UxmlAttribute]
        public float Thickness
        {
            get => thickness;

            set
            {
                thickness = Mathf.Max(0f, value);
                MarkDirtyRepaint();
            }
        }

        /// <summary>Of the outer edge; the inner edge is as much tighter as the ring is thick.</summary>
        [UxmlAttribute]
        public float Radius
        {
            get => radius;

            set
            {
                radius = Mathf.Max(0f, value);
                MarkDirtyRepaint();
            }
        }

        [UxmlAttribute]
        public Color TopColor
        {
            get => topColor;

            set
            {
                topColor = value;
                MarkDirtyRepaint();
            }
        }

        [UxmlAttribute]
        public Color BottomColor
        {
            get => bottomColor;

            set
            {
                bottomColor = value;
                MarkDirtyRepaint();
            }
        }

        public LobbyGradientOutlineElement()
        {
            AddToClassList(USS_BLOCK);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        // The ring is a strip of quads between the outer and the inner path, both run clockwise from the top-left
        // corner, each vertex tinted by its height
        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            float halfExtent = Mathf.Min(rect.width, rect.height) * 0.5f;

            if (halfExtent <= 0f || thickness <= 0f)
                return;

            float outerRadius = Mathf.Min(radius, halfExtent);
            float ringWidth = Mathf.Min(thickness, halfExtent);
            int pointsPerCorner = outerRadius > 0f ? CORNER_SEGMENTS + 1 : 1;
            int pointCount = 4 * pointsPerCorner;

            MeshWriteData mesh = context.Allocate(pointCount * 2, pointCount * 6);

            for (var corner = 0; corner < 4; corner++)
            {
                // Corner centres in the order top-left, top-right, bottom-right, bottom-left, with the angle each arc starts at
                bool right = corner is 1 or 2;
                bool bottom = corner is 2 or 3;
                float centerX = right ? rect.xMax - outerRadius : rect.xMin + outerRadius;
                float centerY = bottom ? rect.yMax - outerRadius : rect.yMin + outerRadius;
                float startAngle = (corner + 2) * 90f;

                for (var segment = 0; segment < pointsPerCorner; segment++)
                {
                    float angle = (startAngle + (pointsPerCorner > 1 ? 90f * segment / (pointsPerCorner - 1) : 45f)) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 outer = new Vector2(centerX, centerY) + (direction * outerRadius);

                    // A corner tighter than the ring is thick has a square inner corner
                    Vector2 inner = outerRadius >= ringWidth
                        ? new Vector2(centerX, centerY) + (direction * (outerRadius - ringWidth))
                        : new Vector2(right ? rect.xMax - ringWidth : rect.xMin + ringWidth, bottom ? rect.yMax - ringWidth : rect.yMin + ringWidth);

                    mesh.SetNextVertex(new Vertex { position = new Vector3(outer.x, outer.y, Vertex.nearZ), tint = ColorAt(outer.y, rect) });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(inner.x, inner.y, Vertex.nearZ), tint = ColorAt(inner.y, rect) });
                }
            }

            for (var i = 0; i < pointCount; i++)
            {
                int next = (i + 1) % pointCount;
                var outerThis = (ushort)(i * 2);
                var innerThis = (ushort)((i * 2) + 1);
                var outerNext = (ushort)(next * 2);
                var innerNext = (ushort)((next * 2) + 1);

                mesh.SetNextIndex(outerThis);
                mesh.SetNextIndex(outerNext);
                mesh.SetNextIndex(innerThis);
                mesh.SetNextIndex(innerThis);
                mesh.SetNextIndex(outerNext);
                mesh.SetNextIndex(innerNext);
            }
        }

        private Color32 ColorAt(float y, Rect rect) =>
            Color.Lerp(topColor, bottomColor, Mathf.InverseLerp(rect.yMin, rect.yMax, y));
    }
}
