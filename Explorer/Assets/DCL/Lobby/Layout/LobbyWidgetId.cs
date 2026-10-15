namespace DCL.Lobby
{
    /// <summary>
    ///     A widget of the lobby layout. Every name ends with the span of the widget in grid cells, columns by rows, which
    ///     <see cref="LobbyWidgetSpans.Span" /> declares; a resized widget gets a new id, so stored layouts drop it.
    /// </summary>
    public enum LobbyWidgetId
    {
        Landing4x3,
        LiveEvents3x3,
        UpcomingEvents3x2,
        FriendsOnline4x2,
        JumpBackIn6x2,
        JumpBackIn4x2,
        RecommendedPlaces6x2,
        RecommendedPlaces4x2,
        Communities6x2,
        Communities4x2,
        Gallery6x2,
        Gallery4x2,
        GoTo4x1,
    }
}
