namespace DCL.Lobby
{
    /// <summary>
    ///     Where in the lobby a card was picked from, reported along the card's own events.
    /// </summary>
    public enum LobbyCardOrigin
    {
        Landing,
        Recent,
        Recommended,
        LiveEvents,
        UpcomingEvents,
    }
}
