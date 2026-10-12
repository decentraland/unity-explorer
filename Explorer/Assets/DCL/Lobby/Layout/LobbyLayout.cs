using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     The placements of the lobby widgets on the four corner grids: at most one placement per widget, none overlapping
    ///     and all inside their grid. Grid sizes are in cells, columns by rows.
    /// </summary>
    public class LobbyLayout
    {
        public const int CORNER_COUNT = 4;

        private static readonly LobbyWidgetPlacement[] DEFAULT_PLACEMENTS =
        {
            new (LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0)),
            new (LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 3)),
            new (LobbyWidgetId.Events3x4, LobbyGridCorner.TopRight, new Vector2Int(1, 0)),
            new (LobbyWidgetId.JumpBackIn6x2, LobbyGridCorner.BottomLeft, new Vector2Int(0, 0)),
            new (LobbyWidgetId.RecommendedPlaces6x2, LobbyGridCorner.BottomRight, new Vector2Int(0, 0)),
        };

        private readonly Vector2Int[] gridSizes = new Vector2Int[CORNER_COUNT];
        private readonly List<LobbyWidgetPlacement> placements = new (DEFAULT_PLACEMENTS.Length);

        public IReadOnlyList<LobbyWidgetPlacement> Placements => placements;

        public LobbyLayout(Vector2Int topLeftGrid, Vector2Int topRightGrid, Vector2Int bottomLeftGrid, Vector2Int bottomRightGrid)
        {
            gridSizes[(int)LobbyGridCorner.TopLeft] = topLeftGrid;
            gridSizes[(int)LobbyGridCorner.TopRight] = topRightGrid;
            gridSizes[(int)LobbyGridCorner.BottomLeft] = bottomLeftGrid;
            gridSizes[(int)LobbyGridCorner.BottomRight] = bottomRightGrid;
        }

        public Vector2Int GridSize(LobbyGridCorner corner) =>
            gridSizes[(int)corner];

        public bool TryGetPlacement(LobbyWidgetId widgetId, out LobbyWidgetPlacement placement)
        {
            int index = IndexOf(widgetId);
            placement = index >= 0 ? placements[index] : default(LobbyWidgetPlacement);
            return index >= 0;
        }

        /// <summary>
        ///     Places the widget, moving it if it is already placed; fails when the cell does not fit the grid or overlaps
        ///     another widget, leaving the layout as it was.
        /// </summary>
        public bool TryPlace(LobbyWidgetId widgetId, LobbyGridCorner corner, Vector2Int cell)
        {
            if (!IsFree(corner, cell, widgetId.Span(), widgetId))
                return false;

            Remove(widgetId);
            placements.Add(new LobbyWidgetPlacement(widgetId, corner, cell));
            return true;
        }

        public bool Remove(LobbyWidgetId widgetId)
        {
            int index = IndexOf(widgetId);

            if (index < 0)
                return false;

            placements.RemoveAt(index);
            return true;
        }

        public void Clear() =>
            placements.Clear();

        /// <summary>
        ///     The placements the lobby ships with; they reproduce the fixed layout the lobby had before the grids.
        /// </summary>
        public void ResetToDefault()
        {
            Clear();

            for (var i = 0; i < DEFAULT_PLACEMENTS.Length; i++)
            {
                LobbyWidgetPlacement placement = DEFAULT_PLACEMENTS[i];
                TryPlace(placement.WidgetId, placement.Corner, placement.Cell);
            }
        }

        /// <summary>
        ///     Whether a widget of the given span fits the grid at the cell without overlapping any placed widget other than
        ///     <paramref name="ignoring" />.
        /// </summary>
        public bool IsFree(LobbyGridCorner corner, Vector2Int cell, Vector2Int span, LobbyWidgetId? ignoring = null)
        {
            Vector2Int grid = gridSizes[(int)corner];

            if (cell.x < 0 || cell.y < 0 || cell.x + span.x > grid.x || cell.y + span.y > grid.y)
                return false;

            for (var i = 0; i < placements.Count; i++)
            {
                LobbyWidgetPlacement other = placements[i];

                if (other.Corner != corner || other.WidgetId == ignoring)
                    continue;

                if (Overlap(cell, span, other.Cell, other.WidgetId.Span()))
                    return false;
            }

            return true;
        }

        /// <summary>
        ///     The free cell closest to the preferred position, which may be fractional and outside the grid: the position is
        ///     rounded and clamped to the cells the span fits in, then the search spreads by Manhattan distance.
        /// </summary>
        public bool TryFindSnapCell(LobbyGridCorner corner, Vector2Int span, Vector2 preferredCell, out Vector2Int cell)
        {
            Vector2Int grid = gridSizes[(int)corner];
            int maxX = grid.x - span.x;
            int maxY = grid.y - span.y;

            if (maxX < 0 || maxY < 0)
            {
                cell = default(Vector2Int);
                return false;
            }

            int originX = Mathf.Clamp(Mathf.FloorToInt(preferredCell.x + 0.5f), 0, maxX);
            int originY = Mathf.Clamp(Mathf.FloorToInt(preferredCell.y + 0.5f), 0, maxY);
            int maxDistance = maxX + maxY;

            for (var distance = 0; distance <= maxDistance; distance++)
            {
                for (int dx = -distance; dx <= distance; dx++)
                {
                    int dy = distance - Math.Abs(dx);
                    cell = new Vector2Int(originX + dx, originY + dy);

                    if (IsFree(corner, cell, span))
                        return true;

                    if (dy == 0)
                        continue;

                    cell = new Vector2Int(originX + dx, originY - dy);

                    if (IsFree(corner, cell, span))
                        return true;
                }
            }

            cell = default(Vector2Int);
            return false;
        }

        private int IndexOf(LobbyWidgetId widgetId)
        {
            for (var i = 0; i < placements.Count; i++)
                if (placements[i].WidgetId == widgetId)
                    return i;

            return -1;
        }

        private static bool Overlap(Vector2Int cellA, Vector2Int spanA, Vector2Int cellB, Vector2Int spanB) =>
            cellA.x < cellB.x + spanB.x && cellB.x < cellA.x + spanA.x &&
            cellA.y < cellB.y + spanB.y && cellB.y < cellA.y + spanA.y;
    }
}
