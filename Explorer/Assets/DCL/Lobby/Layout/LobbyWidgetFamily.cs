namespace DCL.Lobby
{
    /// <summary>
    ///     A widget as the user names it, whatever its size: the widget modal lists one card per family, and a family
    ///     with several sizes places one of its <see cref="LobbyWidgetFamilies.Variants" /> at a time.
    /// </summary>
    public enum LobbyWidgetFamily
    {
        Landing,
        LiveEvents,
        UpcomingEvents,
        FriendsOnline,
        JumpBackIn,
        RecommendedPlaces,
        Communities,
        GoTo,
    }
}
