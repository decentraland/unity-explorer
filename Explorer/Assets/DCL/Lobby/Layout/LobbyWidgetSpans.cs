using System;
using UnityEngine;

namespace DCL.Lobby
{
    public static class LobbyWidgetSpans
    {
        /// <summary>
        ///     The size of the widget in grid cells, columns by rows, as encoded in its name.
        /// </summary>
        public static Vector2Int Span(this LobbyWidgetId widgetId) =>
            widgetId switch
            {
                LobbyWidgetId.Landing4x3 => new Vector2Int(4, 3),
                LobbyWidgetId.LiveEvents3x3 => new Vector2Int(3, 3),
                LobbyWidgetId.UpcomingEvents3x2 => new Vector2Int(3, 2),
                LobbyWidgetId.FriendsOnline4x2 => new Vector2Int(4, 2),
                LobbyWidgetId.JumpBackIn6x2 => new Vector2Int(6, 2),
                LobbyWidgetId.JumpBackIn4x2 => new Vector2Int(4, 2),
                LobbyWidgetId.RecommendedPlaces6x2 => new Vector2Int(6, 2),
                LobbyWidgetId.RecommendedPlaces4x2 => new Vector2Int(4, 2),
                LobbyWidgetId.Communities6x2 => new Vector2Int(6, 2),
                LobbyWidgetId.Communities4x2 => new Vector2Int(4, 2),
                LobbyWidgetId.Gallery6x2 => new Vector2Int(6, 2),
                LobbyWidgetId.Gallery4x2 => new Vector2Int(4, 2),
                LobbyWidgetId.GoTo4x1 => new Vector2Int(4, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(widgetId), widgetId, null),
            };

        /// <summary>How many cards the widget shows at once.</summary>
        public static int CardCount(this LobbyWidgetId widgetId) =>
            widgetId switch
            {
                LobbyWidgetId.Landing4x3 => 1,
                LobbyWidgetId.LiveEvents3x3 => 1,
                LobbyWidgetId.UpcomingEvents3x2 => 1,
                LobbyWidgetId.FriendsOnline4x2 => 4,
                LobbyWidgetId.JumpBackIn6x2 => 3,
                LobbyWidgetId.JumpBackIn4x2 => 2,
                LobbyWidgetId.RecommendedPlaces6x2 => 3,
                LobbyWidgetId.RecommendedPlaces4x2 => 2,
                LobbyWidgetId.Communities6x2 => 3,
                LobbyWidgetId.Communities4x2 => 2,
                LobbyWidgetId.Gallery6x2 => 3,
                LobbyWidgetId.Gallery4x2 => 2,
                LobbyWidgetId.GoTo4x1 => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(widgetId), widgetId, null),
            };
    }
}
