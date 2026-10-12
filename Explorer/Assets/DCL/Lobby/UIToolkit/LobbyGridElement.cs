using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     One of the four corner grids the widgets sit on. The cell unit is shared by every grid and lives here; the UXML
    ///     declares the corner and the cells of each grid and the element sizes itself from them. The cell lines are drawn
    ///     only while editing.
    /// </summary>
    [UxmlElement]
    public partial class LobbyGridElement : VisualElement
    {
        public const float CELL_WIDTH = 136f;
        public const float CELL_HEIGHT = 100f;
        public const float GUTTER = 8f;

        private const string USS_BLOCK = "lobby-grid";
        private const string USS_HIGHLIGHTED = USS_BLOCK + "--highlighted";
        private const string USS_EDITING = USS_BLOCK + "--editing";

        private const float CELL_LINE_WIDTH = 1f;
        private static readonly Color CELL_LINE_COLOR = new (1f, 1f, 1f, 0.15f);

        private int columns = 1;
        private int rows = 1;

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

        /// <summary>Distance between the same corners of two neighbouring cells.</summary>
        public static Vector2 CellPitch => new (CELL_WIDTH + GUTTER, CELL_HEIGHT + GUTTER);

        public LobbyGridElement()
        {
            AddToClassList(USS_BLOCK);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
            Resize();
        }

        /// <summary>Pixel size of a widget spanning the given cells, gutters between them included.</summary>
        public static Vector2 SpanToSize(Vector2Int span) =>
            new (span.x * CELL_WIDTH + (span.x - 1) * GUTTER, span.y * CELL_HEIGHT + (span.y - 1) * GUTTER);

        /// <summary>Position of the top-left corner of the cell in the space of the grid.</summary>
        public static Vector2 CellToLocal(Vector2Int cell) =>
            new (cell.x * (CELL_WIDTH + GUTTER), cell.y * (CELL_HEIGHT + GUTTER));

        /// <summary>The fractional cell whose top-left corner would sit at the position, in the space of the grid.</summary>
        public static Vector2 LocalToCell(Vector2 local) =>
            new (local.x / (CELL_WIDTH + GUTTER), local.y / (CELL_HEIGHT + GUTTER));

        public void SetHighlighted(bool highlighted) =>
            EnableInClassList(USS_HIGHLIGHTED, highlighted);

        public void SetEditing(bool editing)
        {
            IsEditing = editing;
            EnableInClassList(USS_EDITING, editing);
            MarkDirtyRepaint();
        }

        private void Resize()
        {
            Vector2 size = SpanToSize(Size);
            style.width = size.x;
            style.height = size.y;
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (!IsEditing)
                return;

            Painter2D painter = context.painter2D;
            painter.strokeColor = CELL_LINE_COLOR;
            painter.lineWidth = CELL_LINE_WIDTH;
            painter.BeginPath();

            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    Vector2 corner = CellToLocal(new Vector2Int(x, y));
                    painter.MoveTo(corner);
                    painter.LineTo(new Vector2(corner.x + CELL_WIDTH, corner.y));
                    painter.LineTo(new Vector2(corner.x + CELL_WIDTH, corner.y + CELL_HEIGHT));
                    painter.LineTo(new Vector2(corner.x, corner.y + CELL_HEIGHT));
                    painter.ClosePath();
                }
            }

            painter.Stroke();
        }
    }
}
