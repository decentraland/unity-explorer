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
                LobbyWidgetId.Landing4By3 => new Vector2Int(4, 3),
                LobbyWidgetId.LiveEvents3By3 => new Vector2Int(3, 3),
                LobbyWidgetId.UpcomingEvents3By2 => new Vector2Int(3, 2),
                LobbyWidgetId.FriendsOnline4By2 => new Vector2Int(4, 2),
                LobbyWidgetId.JumpBackIn6By2 => new Vector2Int(6, 2),
                LobbyWidgetId.JumpBackIn4By2 => new Vector2Int(4, 2),
                LobbyWidgetId.RecommendedPlaces6By2 => new Vector2Int(6, 2),
                LobbyWidgetId.RecommendedPlaces4By2 => new Vector2Int(4, 2),
                LobbyWidgetId.Communities6By2 => new Vector2Int(6, 2),
                LobbyWidgetId.Communities4By2 => new Vector2Int(4, 2),
                LobbyWidgetId.Gallery6By2 => new Vector2Int(6, 2),
                LobbyWidgetId.Gallery4By2 => new Vector2Int(4, 2),
                LobbyWidgetId.GoTo4By1 => new Vector2Int(4, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(widgetId), widgetId, null),
            };

        /// <summary>How many cards the widget shows at once.</summary>
        public static int CardCount(this LobbyWidgetId widgetId) =>
            widgetId switch
            {
                LobbyWidgetId.Landing4By3 => 1,
                LobbyWidgetId.LiveEvents3By3 => 1,
                LobbyWidgetId.UpcomingEvents3By2 => 1,
                LobbyWidgetId.FriendsOnline4By2 => 4,
                LobbyWidgetId.JumpBackIn6By2 => 3,
                LobbyWidgetId.JumpBackIn4By2 => 2,
                LobbyWidgetId.RecommendedPlaces6By2 => 3,
                LobbyWidgetId.RecommendedPlaces4By2 => 2,
                LobbyWidgetId.Communities6By2 => 3,
                LobbyWidgetId.Communities4By2 => 2,
                LobbyWidgetId.Gallery6By2 => 3,
                LobbyWidgetId.Gallery4By2 => 2,
                LobbyWidgetId.GoTo4By1 => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(widgetId), widgetId, null),
            };
    }
}
