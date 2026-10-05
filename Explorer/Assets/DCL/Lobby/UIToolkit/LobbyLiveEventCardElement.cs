using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Live event card; its hierarchy comes from LobbyLiveEventCard.uxml and LobbyLiveEventCard.uss lays it out.
    ///     The attendees counter is only shown while somebody is there.
    /// </summary>
    [UxmlElement]
    public partial class LobbyLiveEventCardElement : LobbyThumbnailCardElement
    {
        private const string USS_BLOCK = "lobby-live-event-card";
        private const string USS_WITH_ATTENDEES = USS_BLOCK + "--with-attendees";

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

        [UxmlAttribute]
        public int Attendees
        {
            get => attendees;

            set
            {
                attendees = value;
                EnableInClassList(USS_WITH_ATTENDEES, value > 0);

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
