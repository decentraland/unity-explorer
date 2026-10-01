using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     The hero card showing the place the session lands in: a place card with the thumbnail filling it, the online users on top,
    ///     and the title, the creator and the Jump in button along the bottom. Its hierarchy comes from LobbyLandingCard.uxml and
    ///     LobbyLandingCard.uss lays it out. Unlike a row card it can stand for a place that is still unknown or has no details.
    /// </summary>
    [UxmlElement]
    public partial class LobbyLandingCardElement : LobbyPlaceCardElement
    {
        private const string USS_BLOCK = "lobby-landing-card";
        private const string USS_WITH_ONLINE = USS_BLOCK + "--with-online";
        private const string USS_STATIC = USS_BLOCK + "--static";

        private bool canJumpIn = true;

        /// <summary>
        ///     The online counter is hidden while false, as while the place is still unknown.
        /// </summary>
        [UxmlAttribute]
        public bool HasOnlineCount
        {
            get => ClassListContains(USS_WITH_ONLINE);
            set => EnableInClassList(USS_WITH_ONLINE, value);
        }

        /// <summary>
        ///     The Jump in button is disabled while false, as while the place is still unknown.
        /// </summary>
        [UxmlAttribute]
        public bool CanJumpIn
        {
            get => canJumpIn;

            set
            {
                canJumpIn = value;
                jumpInButton?.SetEnabled(value);
            }
        }

        /// <summary>
        ///     The card takes no click and is marked static, which the stylesheet dims, while false: while the place is still unknown,
        ///     or when it has no details to open.
        /// </summary>
        [UxmlAttribute]
        public bool CanOpen
        {
            get => !ClassListContains(USS_STATIC);
            set => EnableInClassList(USS_STATIC, !value);
        }

        protected override bool canClick => CanOpen;

        public LobbyLandingCardElement() : base(USS_BLOCK) { }

        protected override void ResolveChildren()
        {
            base.ResolveChildren();
            CanJumpIn = canJumpIn;
        }
    }
}
