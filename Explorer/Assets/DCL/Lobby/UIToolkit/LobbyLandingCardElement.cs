using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Hero card of the landing place. Unlike a row card it can stand for a place still unknown or without details.
    /// </summary>
    [UxmlElement]
    public partial class LobbyLandingCardElement : LobbyPlaceCardElement
    {
        private const string USS_BLOCK = "lobby-landing-card";
        private const string USS_STATIC = USS_BLOCK + "--static";

        private bool canJumpIn = true;

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
        ///     While false the card takes no click, shows the plain cursor and is marked static, dimmed by USS.
        /// </summary>
        [UxmlAttribute]
        public bool CanOpen
        {
            get => !ClassListContains(USS_STATIC);

            set
            {
                EnableInClassList(USS_STATIC, !value);
                EnableInClassList(VisualElementsExtensions.INTERACTABLE_CLASS, value);
            }
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
