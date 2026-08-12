namespace DCL.Chat
{
    /// <summary>
    /// Tracks the username/wallet/official actually last rendered into a chat entry so the profile
    /// update callback re-renders only on a genuine change from what is currently on screen. It
    /// compares against the LAST-RENDERED value (never the immutable message snapshot), so a
    /// name -> A -> name round-trip still re-renders the final revert instead of stranding "A".
    /// </summary>
    internal struct RenderedNameGate
    {
        private string? userName;
        private string? userWalletId;
        private bool isOfficial;

        /// <summary>Seed the gate with what the full-bind snapshot render put on screen.</summary>
        public void SetRendered(string? renderedUserName, string? renderedUserWalletId, bool renderedIsOfficial)
        {
            userName = renderedUserName;
            userWalletId = renderedUserWalletId;
            isOfficial = renderedIsOfficial;
        }

        /// <summary>
        /// Returns true (and records the incoming values as the new rendered state) when the incoming
        /// profile differs from what was last rendered; false when it already matches the screen.
        /// </summary>
        public bool ShouldRender(string? incomingUserName, string? incomingUserWalletId, bool incomingIsOfficial)
        {
            if (incomingUserName == userName && incomingUserWalletId == userWalletId && incomingIsOfficial == isOfficial)
                return false;

            userName = incomingUserName;
            userWalletId = incomingUserWalletId;
            isOfficial = incomingIsOfficial;
            return true;
        }
    }
}
