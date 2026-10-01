using System;
using System.Threading;

namespace DCL.Lobby
{
    public readonly struct LobbyParameter
    {
        /// <summary>
        ///     True when the lobby gates the startup flow, where Jump in is the only way out and no close button is offered.
        /// </summary>
        public readonly bool IsStartup;

        /// <summary>
        ///     Releases the startup flow; leaving the screen is not enough, as a fullscreen panel opened from the lobby replaces it and it comes back.
        /// </summary>
        public readonly Action? JumpedIn;

        /// <summary>
        ///     Cancelled when a Logout takes the startup flow over, so the lobby must not show itself again.
        /// </summary>
        public readonly CancellationToken StartupToken;

        public LobbyParameter(bool isStartup, Action? jumpedIn = null, CancellationToken startupToken = default)
        {
            IsStartup = isStartup;
            JumpedIn = jumpedIn;
            StartupToken = startupToken;
        }
    }

    /// <summary>
    ///     The MVC manager keys controllers by view and input type, so this distinct type registers the lobby's popups next to the sidebar's.
    /// </summary>
    public readonly struct LobbyPopupParameter { }
}
