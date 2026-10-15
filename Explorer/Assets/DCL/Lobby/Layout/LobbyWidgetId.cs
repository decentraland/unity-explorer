namespace DCL.Lobby
{
    /// <summary>
    ///     A widget of the lobby layout. Every name ends with the span of the widget in grid cells, columns by rows, which
    ///     <see cref="LobbyWidgetSpans.Span" /> declares; a resized widget gets a new id, so stored layouts drop it.
    /// </summary>
    public enum LobbyWidgetId
    {
        Landing4By3,
        LiveEvents3By3,
        UpcomingEvents3By2,
        FriendsOnline4By2,
        JumpBackIn6By2,
        JumpBackIn4By2,
        RecommendedPlaces6By2,
        RecommendedPlaces4By2,
        Communities6By2,
        Communities4By2,
        Gallery6By2,
        Gallery4By2,
        GoTo4By1,
    }
}
