using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     Where a widget sits: the grid and the cell under its top-left corner.
    /// </summary>
    public readonly struct LobbyWidgetPlacement
    {
        public readonly LobbyWidgetId WidgetId;
        public readonly LobbyGridCorner Corner;
        public readonly Vector2Int Cell;

        public LobbyWidgetPlacement(LobbyWidgetId widgetId, LobbyGridCorner corner, Vector2Int cell)
        {
            WidgetId = widgetId;
            Corner = corner;
            Cell = cell;
        }
    }
}
