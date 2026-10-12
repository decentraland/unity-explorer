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
            new (LobbyWidgetId.LiveEvents3x3, LobbyGridCorner.TopRight, new Vector2Int(1, 0)),
            new (LobbyWidgetId.UpcomingEvents3x2, LobbyGridCorner.TopRight, new Vector2Int(1, 3)),
            new (LobbyWidgetId.JumpBackIn6x2, LobbyGridCorner.BottomLeft, new Vector2Int(0, 0)),
            new (LobbyWidgetId.RecommendedPlaces6x2, LobbyGridCorner.BottomRight, new Vector2Int(0, 0)),
        };

        /// <summary>Down, right, up and left in grid space; the order breaks ties between equally cheap pushes.</summary>
        private static readonly Vector2Int[] PUSH_DIRECTIONS = { new (0, 1), new (1, 0), new (0, -1), new (-1, 0) };

        private readonly Vector2Int[] gridSizes = new Vector2Int[CORNER_COUNT];
        private readonly List<LobbyWidgetPlacement> placements = new (DEFAULT_PLACEMENTS.Length);
        private readonly List<LobbyWidgetPlacement> pushScratch = new (DEFAULT_PLACEMENTS.Length);
        private readonly List<int> pushQueue = new (DEFAULT_PLACEMENTS.Length);

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

        /// <summary>
        ///     Places the widget like <see cref="TryPlace" />, but the widgets in its way make room instead of blocking it:
        ///     they move aside as <see cref="TryResolvePush" /> resolves. Fails the same way, leaving the layout as it was.
        /// </summary>
        public bool TryPlacePushing(LobbyWidgetId widgetId, LobbyGridCorner corner, Vector2Int cell)
        {
            if (!TryResolvePush(widgetId, corner, cell, pushScratch))
                return false;

            for (var i = 0; i < pushScratch.Count; i++)
                placements[IndexOf(pushScratch[i].WidgetId)] = pushScratch[i];

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
            if (!Fits(gridSizes[(int)corner], cell, span))
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
        ///     Works out where the widgets of the grid end up if the widget takes the cell and whatever it overlaps moves off
        ///     the side it covers least, by as many cells as clear it: right when it covers their left columns, down when it
        ///     covers their top rows, and so on. A side the grid edge, the placed widget or a widget this resolution already
        ///     moved blocks is passed over for the next cheapest. Every moved widget pushes in turn whatever it lands on, and
        ///     moves only once. Fills <paramref name="pushed" /> with the new placements of the widgets that move, none when
        ///     the cell is free. Fails, leaving it empty, when the widget does not fit the grid or a widget in the way has no
        ///     side left to move to. The layout itself does not change.
        /// </summary>
        public bool TryResolvePush(LobbyWidgetId widgetId, LobbyGridCorner corner, Vector2Int cell, List<LobbyWidgetPlacement> pushed)
        {
            pushed.Clear();
            Vector2Int grid = gridSizes[(int)corner];
            Vector2Int span = widgetId.Span();

            if (!Fits(grid, cell, span))
                return false;

            for (var i = 0; i < placements.Count; i++)
                if (placements[i].Corner == corner && placements[i].WidgetId != widgetId)
                    pushed.Add(placements[i]);

            // The placed widget pushes first, as the entry before the list; every widget it pushes queues to push in turn.
            // A bit per entry marks the widgets moved so far, which stay put from then on
            const int PLACED = -1;
            pushQueue.Clear();
            pushQueue.Add(PLACED);
            var moved = 0;

            for (var head = 0; head < pushQueue.Count; head++)
            {
                int pusher = pushQueue[head];
                Vector2Int pusherCell = pusher == PLACED ? cell : pushed[pusher].Cell;
                Vector2Int pusherSpan = pusher == PLACED ? span : pushed[pusher].WidgetId.Span();

                for (var i = 0; i < pushed.Count; i++)
                {
                    if (i == pusher)
                        continue;

                    LobbyWidgetPlacement other = pushed[i];
                    Vector2Int otherSpan = other.WidgetId.Span();

                    if (!Overlap(pusherCell, pusherSpan, other.Cell, otherSpan))
                        continue;

                    if ((moved & (1 << i)) != 0 || !TryPushAway(grid, cell, span, pushed, moved, i, pusherCell, pusherSpan, out Vector2Int pushedCell))
                    {
                        pushed.Clear();
                        return false;
                    }

                    pushed[i] = new LobbyWidgetPlacement(other.WidgetId, corner, pushedCell);
                    moved |= 1 << i;
                    pushQueue.Add(i);
                }
            }

            for (int i = pushed.Count - 1; i >= 0; i--)
                if ((moved & (1 << i)) == 0)
                    pushed.RemoveAt(i);

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

        /// <summary>
        ///     The cheapest side of the pusher the widget at <paramref name="index" /> can move off to: the one that takes the
        ///     fewest cells among those where it fits the grid and overlaps neither the placed widget nor a moved one.
        /// </summary>
        private static bool TryPushAway(Vector2Int grid, Vector2Int placedCell, Vector2Int placedSpan, List<LobbyWidgetPlacement> pushed, int moved, int index,
            Vector2Int pusherCell, Vector2Int pusherSpan, out Vector2Int pushedCell)
        {
            Vector2Int otherCell = pushed[index].Cell;
            Vector2Int otherSpan = pushed[index].WidgetId.Span();
            var best = int.MaxValue;
            pushedCell = default(Vector2Int);

            for (var d = 0; d < PUSH_DIRECTIONS.Length; d++)
            {
                Vector2Int direction = PUSH_DIRECTIONS[d];
                int displacement = Displacement(direction, pusherCell, pusherSpan, otherCell, otherSpan);

                if (displacement >= best)
                    continue;

                Vector2Int candidate = otherCell + (direction * displacement);

                if (!Fits(grid, candidate, otherSpan) || Overlap(candidate, otherSpan, placedCell, placedSpan) || OverlapsMoved(pushed, moved, index, candidate, otherSpan))
                    continue;

                best = displacement;
                pushedCell = candidate;
            }

            return best != int.MaxValue;
        }

        /// <summary>How many cells the widget must travel in the direction to clear the pusher it overlaps.</summary>
        private static int Displacement(Vector2Int direction, Vector2Int pusherCell, Vector2Int pusherSpan, Vector2Int otherCell, Vector2Int otherSpan)
        {
            if (direction.y > 0) return pusherCell.y + pusherSpan.y - otherCell.y;
            if (direction.y < 0) return otherCell.y + otherSpan.y - pusherCell.y;
            if (direction.x > 0) return pusherCell.x + pusherSpan.x - otherCell.x;

            return otherCell.x + otherSpan.x - pusherCell.x;
        }

        private static bool OverlapsMoved(List<LobbyWidgetPlacement> pushed, int moved, int index, Vector2Int cell, Vector2Int span)
        {
            for (var i = 0; i < pushed.Count; i++)
                if (i != index && (moved & (1 << i)) != 0 && Overlap(cell, span, pushed[i].Cell, pushed[i].WidgetId.Span()))
                    return true;

            return false;
        }

        private static bool Fits(Vector2Int grid, Vector2Int cell, Vector2Int span) =>
            cell.x >= 0 && cell.y >= 0 && cell.x + span.x <= grid.x && cell.y + span.y <= grid.y;

        private static bool Overlap(Vector2Int cellA, Vector2Int spanA, Vector2Int cellB, Vector2Int spanB) =>
            cellA.x < cellB.x + spanB.x && cellB.x < cellA.x + spanA.x &&
            cellA.y < cellB.y + spanB.y && cellB.y < cellA.y + spanA.y;
    }
}
