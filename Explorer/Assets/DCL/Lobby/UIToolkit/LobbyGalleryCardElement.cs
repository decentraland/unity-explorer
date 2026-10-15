using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Card of one of the user's camera reel photos; its hierarchy comes from LobbyGalleryCard.uxml, laid out and
    ///     hover-animated by LobbyGalleryCard.uss. The photo fills the card over a caption with the day it was taken.
    /// </summary>
    [UxmlElement]
    public partial class LobbyGalleryCardElement : LobbyThumbnailCardElement
    {
        private const string USS_BLOCK = "lobby-gallery-card";
        private const string DATE_NAME = "Date";

        private Label? dateLabel;

        private string dateText = string.Empty;

        [UxmlAttribute]
        public string DateText
        {
            get => dateText;

            set
            {
                dateText = value;

                if (dateLabel != null)
                    dateLabel.text = value;
            }
        }

        public LobbyGalleryCardElement() : base(USS_BLOCK) { }

        protected override void ResolveChildren()
        {
            dateLabel = this.Q<Label>(DATE_NAME);
            dateLabel.text = dateText;
        }
    }
}
