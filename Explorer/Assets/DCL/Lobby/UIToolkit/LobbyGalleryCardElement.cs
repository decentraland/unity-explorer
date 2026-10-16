using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Card of one of the user's camera reel photos; its hierarchy comes from LobbyGalleryCard.uxml, laid out and
    ///     hover-animated by LobbyGalleryCard.uss. The photo fills the card with nothing drawn over it.
    /// </summary>
    [UxmlElement]
    public partial class LobbyGalleryCardElement : LobbyThumbnailCardElement
    {
        private const string USS_BLOCK = "lobby-gallery-card";

        public LobbyGalleryCardElement() : base(USS_BLOCK) { }

        protected override void ResolveChildren() { }
    }
}
