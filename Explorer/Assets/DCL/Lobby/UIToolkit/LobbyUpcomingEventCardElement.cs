using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Upcoming event card; its hierarchy comes from LobbyUpcomingEventCard.uxml, laid out and hover-animated by
    ///     LobbyUpcomingEventCard.uss.
    /// </summary>
    [UxmlElement]
    public partial class LobbyUpcomingEventCardElement : LobbyThumbnailCardElement
    {
        private const string USS_BLOCK = "lobby-upcoming-event-card";
        private const string USS_INTERESTED = USS_BLOCK + "--interested";

        private const string TITLE_NAME = "Title";
        private const string HOST_NAME = "Host";
        private const string STARTS_IN_NAME = "StartsIn";
        private const string INTERESTED_NAME = "Interested";
        private const string ADD_TO_CALENDAR_NAME = "AddToCalendar";
        private const string SHARE_NAME = "Share";

        public Action? InterestedClicked;
        public Action? AddToCalendarClicked;
        public Action? ShareClicked;

        private Label? titleLabel;
        private Label? hostLabel;
        private Label? startsInLabel;
        private Button? interestedButton;
        private Button? addToCalendarButton;
        private Button? shareButton;

        private string title = string.Empty;
        private string host = string.Empty;
        private string startsIn = string.Empty;

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
        public string StartsIn
        {
            get => startsIn;

            set
            {
                startsIn = value;

                if (startsInLabel != null)
                    startsInLabel.text = value;
            }
        }

        public bool IsInterested
        {
            get => ClassListContains(USS_INTERESTED);
            set => EnableInClassList(USS_INTERESTED, value);
        }

        /// <summary>
        ///     Centre of the Share button in screen pixels; only meaningful while the card is on a panel.
        /// </summary>
        public Vector2 ShareButtonScreenPosition => shareButton!.ScreenCenter();

        public LobbyUpcomingEventCardElement() : base(USS_BLOCK) { }

        protected override void ResolveChildren()
        {
            titleLabel = this.Q<Label>(TITLE_NAME);
            hostLabel = this.Q<Label>(HOST_NAME);
            startsInLabel = this.Q<Label>(STARTS_IN_NAME);
            interestedButton = this.Q<Button>(INTERESTED_NAME);
            addToCalendarButton = this.Q<Button>(ADD_TO_CALENDAR_NAME);
            shareButton = this.Q<Button>(SHARE_NAME);

            interestedButton.clicked += OnInterestedClicked;
            addToCalendarButton.clicked += OnAddToCalendarClicked;
            shareButton.clicked += OnShareClicked;

            titleLabel.text = title;
            hostLabel.text = host;
            startsInLabel.text = startsIn;
        }

        private void OnInterestedClicked() =>
            InterestedClicked?.Invoke();

        private void OnAddToCalendarClicked() =>
            AddToCalendarClicked?.Invoke();

        private void OnShareClicked() =>
            ShareClicked?.Invoke();
    }
}
