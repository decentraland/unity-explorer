using System;
using System.Threading;

namespace DCL.Lobby
{
    public readonly struct LobbyParameter
    {
        public readonly bool IsStartup;

        /// <summary>
        ///     Releases the startup flow, which hiding the lobby alone does not.
        /// </summary>
        public readonly Action? JumpedIn;

        /// <summary>
        ///     Cancelled when a Logout takes the startup flow over.
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
    ///     Distinct input type, so the MVC manager (keyed by view and input type) registers the lobby's popups too.
    /// </summary>
    public readonly struct LobbyPopupParameter { }
}
