using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Full-bleed thumbnail with the Live badge and how many people
    ///     are attending on top, plus the name and the host over the gradient at the bottom. Its hierarchy comes from LobbyLiveEventCard.uxml;
    ///     the badges and the gradient are laid out by LobbyLiveEventCard.uss.
    /// </summary>
    [UxmlElement]
    public partial class LobbyLiveEventCardElement : LobbyThumbnailCardElement
    {
        private const string USS_BLOCK = "lobby-live-event-card";

        private const string TITLE_NAME = "Title";
        private const string HOST_NAME = "Host";
        private const string ATTENDEES_NAME = "Attendees";

        private Label? titleLabel;
        private Label? hostLabel;
        private Label? attendeesLabel;

        private string title = string.Empty;
        private string host = string.Empty;
        private int attendees;

        [UxmlAttribute]
        public string Title
        {
            get => title;

            set
            {
                title = value;

                if (titleLabel != null)
                    titleLabel.text = value;
            }
        }

        /// <summary>
        ///     Shown as given, under the title.
        /// </summary>
        [UxmlAttribute]
        public string Host
        {
            get => host;

            set
            {
                host = value;

                if (hostLabel != null)
                    hostLabel.text = value;
            }
        }

        /// <summary>
        ///     How many people are attending the event right now.
        /// </summary>
        [UxmlAttribute]
        public int Attendees
        {
            get => attendees;

            set
            {
                attendees = value;

                if (attendeesLabel != null)
                    attendeesLabel.text = value.ToString();
            }
        }

        public LobbyLiveEventCardElement() : base(USS_BLOCK) { }

        protected override void ResolveChildren()
        {
            titleLabel = this.Q<Label>(TITLE_NAME);
            hostLabel = this.Q<Label>(HOST_NAME);
            attendeesLabel = this.Q<Label>(ATTENDEES_NAME);

            titleLabel.text = title;
            hostLabel.text = host;
            attendeesLabel.text = attendees.ToString();
        }
    }
}
