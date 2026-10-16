using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     One of the four corner grids the widgets sit on. The cell unit, the square cell of the design, is shared by every grid and lives here; the UXML
    ///     declares the corner and the cells of each grid and the element sizes itself from them. While editing, a backdrop
    ///     tints the area of the grid and anchor dots are drawn just inside the corners of every cell, so the dots of
    ///     neighbouring cells stay apart across the gutter; the dots of the cells a drop would take are tinted by whether it
    ///     can take them.
    /// </summary>
    [UxmlElement]
    public partial class LobbyGridElement : VisualElement
    {
        public const float CELL_SIZE = 114f;
        public const float GUTTER = 2f;

        private const string USS_BLOCK = "lobby-grid";
        private const string USS_HIGHLIGHTED = USS_BLOCK + "--highlighted";
        private const string USS_EDITING = USS_BLOCK + "--editing";
        private const string USS_BACKDROP = USS_BLOCK + "__backdrop";

        private const float ANCHOR_DOT_RADIUS = 1f;
        private const float ANCHOR_DOT_INSET = 1.5f;
        private static readonly Color ANCHOR_DOT_COLOR = new (1f, 1f, 1f, 0.6f);
        private static readonly Color VALID_DOT_COLOR = new (52f / 255f, 206f / 255f, 119f / 255f);
        private static readonly Color INVALID_DOT_COLOR = new (1f, 45f / 255f, 85f / 255f);

        private int columns = 1;
        private int rows = 1;
        private RectInt dropHint;
        private Color dropHintColor;

        [UxmlAttribute]
        public LobbyGridCorner Corner { get; set; }

        [UxmlAttribute]
        public int Columns
        {
            get => columns;

            set
            {
                columns = Mathf.Max(1, value);
                Resize();
            }
        }

        [UxmlAttribute]
        public int Rows
        {
            get => rows;

            set
            {
                rows = Mathf.Max(1, value);
                Resize();
            }
        }

        /// <summary>Columns by rows.</summary>
        public Vector2Int Size => new (columns, rows);

        public bool IsEditing { get; private set; }

        /// <summary>Whether the dots of some cells are tinted for a drop (see <see cref="SetDropHint" />).</summary>
        public bool HasDropHint { get; private set; }

        /// <summary>Distance between the same corners of two neighbouring cells.</summary>
        public static Vector2 CellPitch => new (CELL_SIZE + GUTTER, CELL_SIZE + GUTTER);

        public LobbyGridElement()
        {
            AddToClassList(USS_BLOCK);
            pickingMode = PickingMode.Ignore;

            var backdrop = new VisualElement { name = "Backdrop", pickingMode = PickingMode.Ignore };
            backdrop.AddToClassList(USS_BACKDROP);
            Add(backdrop);

            generateVisualContent += OnGenerateVisualContent;
            Resize();
        }

        /// <summary>Pixel size of a widget spanning the given cells, gutters between them included.</summary>
        public static Vector2 SpanToSize(Vector2Int span) =>
            new ((span.x * CELL_SIZE) + ((span.x - 1) * GUTTER), (span.y * CELL_SIZE) + ((span.y - 1) * GUTTER));

        /// <summary>Position of the top-left corner of the cell in the space of the grid.</summary>
        public static Vector2 CellToLocal(Vector2Int cell) =>
            new (cell.x * (CELL_SIZE + GUTTER), cell.y * (CELL_SIZE + GUTTER));

        /// <summary>The fractional cell whose top-left corner would sit at the position, in the space of the grid.</summary>
        public static Vector2 LocalToCell(Vector2 local) =>
            new (local.x / (CELL_SIZE + GUTTER), local.y / (CELL_SIZE + GUTTER));

        /// <summary>The cell whose top-left corner is nearest to the position; it may lie outside the grid.</summary>
        public static Vector2Int NearestCell(Vector2 local)
        {
            Vector2 cell = LocalToCell(local);
            return new Vector2Int(Mathf.RoundToInt(cell.x), Mathf.RoundToInt(cell.y));
        }

        /// <summary>Brightens the backdrop while a drag targets the grid.</summary>
        public void SetHighlighted(bool highlighted) =>
            EnableInClassList(USS_HIGHLIGHTED, highlighted);

        public void SetEditing(bool editing)
        {
            IsEditing = editing;
            EnableInClassList(USS_EDITING, editing);

            if (!editing)
                HasDropHint = false;

            MarkDirtyRepaint();
        }

        /// <summary>
        ///     Tints the dots of the cells a widget of the span would take at the cell: green when the drop can take them,
        ///     red when it cannot. Shows only while editing.
        /// </summary>
        public void SetDropHint(Vector2Int cell, Vector2Int span, bool valid)
        {
            HasDropHint = true;
            dropHint = new RectInt(cell, span);
            dropHintColor = valid ? VALID_DOT_COLOR : INVALID_DOT_COLOR;
            MarkDirtyRepaint();
        }

        public void ClearDropHint()
        {
            if (!HasDropHint)
                return;

            HasDropHint = false;
            MarkDirtyRepaint();
        }

        private void Resize()
        {
            Vector2 size = SpanToSize(Size);
            style.width = size.x;
            style.height = size.y;
        }

        // The fill colour is one per path, so the tinted dots go in a pass of their own
        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (!IsEditing)
                return;

            Painter2D painter = context.painter2D;
            PaintDots(painter, ANCHOR_DOT_COLOR, hinted: false);

            if (HasDropHint)
                PaintDots(painter, dropHintColor, hinted: true);
        }

        /// <summary>Draws the four dots of every cell inside the drop hint, or of every cell outside it.</summary>
        private void PaintDots(Painter2D painter, Color color, bool hinted)
        {
            painter.fillColor = color;
            painter.BeginPath();

            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
            {
                if (IsHinted(x, y) != hinted)
                    continue;

                Vector2 corner = CellToLocal(new Vector2Int(x, y));
                float left = corner.x + ANCHOR_DOT_INSET;
                float right = corner.x + CELL_SIZE - ANCHOR_DOT_INSET;
                float top = corner.y + ANCHOR_DOT_INSET;
                float bottom = corner.y + CELL_SIZE - ANCHOR_DOT_INSET;

                AddDot(painter, new Vector2(left, top));
                AddDot(painter, new Vector2(right, top));
                AddDot(painter, new Vector2(right, bottom));
                AddDot(painter, new Vector2(left, bottom));
            }

            painter.Fill();
        }

        private bool IsHinted(int x, int y) =>
            HasDropHint && dropHint.Contains(new Vector2Int(x, y));

        // Each dot is its own closed sub-path so the fill does not join it to the previous one
        private static void AddDot(Painter2D painter, Vector2 center)
        {
            painter.MoveTo(new Vector2(center.x + ANCHOR_DOT_RADIUS, center.y));
            painter.Arc(center, ANCHOR_DOT_RADIUS, 0f, 360f);
            painter.ClosePath();
        }
    }
}