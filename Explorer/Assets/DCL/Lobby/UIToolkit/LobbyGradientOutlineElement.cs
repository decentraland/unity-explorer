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

        /// <summary>A fade on either side of the solid core: a mesh gets no anti-aliasing of its own.</summary>
        private const int VERTICES_PER_POINT = 4;

        private const int INDICES_PER_POINT = (VERTICES_PER_POINT - 1) * 6;

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

        // The ring is three strips of quads between the outer and the inner path, both run clockwise from the top-left
        // corner, each vertex tinted by its height; the fades straddle the edges so the ring keeps its width.
        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            float halfExtent = Mathf.Min(rect.width, rect.height) * 0.5f;

            if (halfExtent <= 0f || thickness <= 0f)
                return;

            float outerRadius = Mathf.Min(radius, halfExtent);
            float ringWidth = Mathf.Min(thickness, halfExtent);
            float halfFeather = Mathf.Min(0.5f / scaledPixelsPerPoint, ringWidth * 0.5f);
            int pointsPerCorner = outerRadius > 0f ? CORNER_SEGMENTS + 1 : 1;
            int pointCount = 4 * pointsPerCorner;

            MeshWriteData mesh = context.Allocate(pointCount * VERTICES_PER_POINT, pointCount * INDICES_PER_POINT);

            for (var corner = 0; corner < 4; corner++)
            {
                // Corner centres in the order top-left, top-right, bottom-right, bottom-left, with the angle each arc starts at
                bool right = corner is 1 or 2;
                bool bottom = corner is 2 or 3;
                float centerX = right ? rect.xMax - outerRadius : rect.xMin + outerRadius;
                float centerY = bottom ? rect.yMax - outerRadius : rect.yMin + outerRadius;
                float startAngle = (corner + 2) * 90f;

                // A square corner fades along its diagonal so the band keeps its width on both edges.
                var cornerDiagonal = new Vector2(right ? 1f : -1f, bottom ? 1f : -1f);

                for (var segment = 0; segment < pointsPerCorner; segment++)
                {
                    float angle = (startAngle + (pointsPerCorner > 1 ? 90f * segment / (pointsPerCorner - 1) : 45f)) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 outer = new Vector2(centerX, centerY) + (direction * outerRadius);
                    Vector2 outerNormal = pointsPerCorner > 1 ? direction : cornerDiagonal;

                    // A corner tighter than the ring is thick has a square inner corner
                    bool roundedInner = outerRadius >= ringWidth;

                    Vector2 inner = roundedInner
                        ? new Vector2(centerX, centerY) + (direction * (outerRadius - ringWidth))
                        : new Vector2(right ? rect.xMax - ringWidth : rect.xMin + ringWidth, bottom ? rect.yMax - ringWidth : rect.yMin + ringWidth);

                    Vector2 innerNormal = roundedInner ? direction : cornerDiagonal;

                    AddVertex(mesh, outer + (outerNormal * halfFeather), rect, 0);
                    AddVertex(mesh, outer - (outerNormal * halfFeather), rect, byte.MaxValue);
                    AddVertex(mesh, inner + (innerNormal * halfFeather), rect, byte.MaxValue);
                    AddVertex(mesh, inner - (innerNormal * halfFeather), rect, 0);
                }
            }

            for (var i = 0; i < pointCount; i++)
            {
                int next = (i + 1) % pointCount;

                for (var strip = 0; strip < VERTICES_PER_POINT - 1; strip++)
                {
                    var nearThis = (ushort)((i * VERTICES_PER_POINT) + strip);
                    var farThis = (ushort)(nearThis + 1);
                    var nearNext = (ushort)((next * VERTICES_PER_POINT) + strip);
                    var farNext = (ushort)(nearNext + 1);

                    mesh.SetNextIndex(nearThis);
                    mesh.SetNextIndex(nearNext);
                    mesh.SetNextIndex(farThis);
                    mesh.SetNextIndex(farThis);
                    mesh.SetNextIndex(nearNext);
                    mesh.SetNextIndex(farNext);
                }
            }
        }

        private void AddVertex(MeshWriteData mesh, Vector2 position, Rect rect, byte alpha)
        {
            Color32 tint = ColorAt(position.y, rect);
            tint.a = alpha;
            mesh.SetNextVertex(new Vertex { position = new Vector3(position.x, position.y, Vertex.nearZ), tint = tint });
        }

        private Color32 ColorAt(float y, Rect rect) =>
            Color.Lerp(topColor, bottomColor, Mathf.InverseLerp(rect.yMin, rect.yMax, y));
    }
}
