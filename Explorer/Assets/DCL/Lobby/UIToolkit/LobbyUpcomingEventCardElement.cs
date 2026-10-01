using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Upcoming event card modelled on the small Explore event card: name, host and how long
    ///     until the event starts on the left, the thumbnail on the right, plus the Add to calendar and Share buttons. Its hierarchy comes
    ///     from LobbyUpcomingEventCard.uxml, so the children only exist once that template is instantiated; LobbyUpcomingEventCard.uss
    ///     lays it out and tints the events the user is interested in, whose Interested button shows as toggled on.
    /// </summary>
    [UxmlElement]
    public partial class LobbyUpcomingEventCardElement : VisualElement, ILobbyThumbnailCard
    {
        private const string USS_BLOCK = "lobby-upcoming-event-card";
        private const string USS_LOADING = USS_BLOCK + "--loading";
        private const string USS_INTERESTED = USS_BLOCK + "--interested";

        private const string TITLE_NAME = "Title";
        private const string HOST_NAME = "Host";
        private const string STARTS_IN_NAME = "StartsIn";
        private const string THUMBNAIL_NAME = "Thumbnail";
        private const string INTERESTED_NAME = "Interested";
        private const string ADD_TO_CALENDAR_NAME = "AddToCalendar";
        private const string SHARE_NAME = "Share";

        /// <summary>
        ///     Raised on a click anywhere on the card except its buttons.
        /// </summary>
        public Action? Clicked;

        public Action? InterestedClicked;
        public Action? AddToCalendarClicked;
        public Action? ShareClicked;

        private Label? titleLabel;
        private Label? hostLabel;
        private Label? startsInLabel;
        private VisualElement? thumbnail;
        private Button? interestedButton;
        private Button? addToCalendarButton;
        private Button? shareButton;

        // Attribute values and the thumbnail can arrive before the template children do; they are applied once those attach
        private string title = string.Empty;
        private string host = string.Empty;
        private string startsIn = string.Empty;
        private Sprite? thumbnailSprite;

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
        ///     How long until the event starts, shown as given next to the clock.
        /// </summary>
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

        public Sprite? Thumbnail
        {
            get => thumbnailSprite;

            set
            {
                thumbnailSprite = value;

                if (thumbnail != null)
                    ApplyThumbnail();
            }
        }

        public bool IsLoading
        {
            get => ClassListContains(USS_LOADING);
            set => EnableInClassList(USS_LOADING, value);
        }

        /// <summary>
        ///     The user marked the event as one they are interested in, which tints the card.
        /// </summary>
        public bool IsInterested
        {
            get => ClassListContains(USS_INTERESTED);
            set => EnableInClassList(USS_INTERESTED, value);
        }

        /// <summary>
        ///     Centre of the Share button in screen pixels, where a uGUI context menu is anchored. Only meaningful while the card is on a panel.
        /// </summary>
        public Vector2 ShareButtonScreenPosition => shareButton!.ScreenCenter();

        public LobbyUpcomingEventCardElement()
        {
            AddToClassList(USS_BLOCK);
            this.AddManipulator(new Clickable(OnClicked));
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent _)
        {
            // The same element can be detached and attached again; its children are resolved only the first time
            if (titleLabel != null)
                return;

            titleLabel = this.Q<Label>(TITLE_NAME);
            hostLabel = this.Q<Label>(HOST_NAME);
            startsInLabel = this.Q<Label>(STARTS_IN_NAME);
            thumbnail = this.Q<VisualElement>(THUMBNAIL_NAME);
            interestedButton = this.Q<Button>(INTERESTED_NAME);
            addToCalendarButton = this.Q<Button>(ADD_TO_CALENDAR_NAME);
            shareButton = this.Q<Button>(SHARE_NAME);

            interestedButton.clicked += OnInterestedClicked;
            addToCalendarButton.clicked += OnAddToCalendarClicked;
            shareButton.clicked += OnShareClicked;

            titleLabel.text = title;
            hostLabel.text = host;
            startsInLabel.text = startsIn;
            ApplyThumbnail();
        }

        private void ApplyThumbnail()
        {
            thumbnail!.style.backgroundImage = LobbyCardBackground.From(thumbnailSprite);
        }

        private void OnClicked() =>
            Clicked?.Invoke();

        private void OnInterestedClicked() =>
            InterestedClicked?.Invoke();

        private void OnAddToCalendarClicked() =>
            AddToCalendarClicked?.Invoke();

        private void OnShareClicked() =>
            ShareClicked?.Invoke();
    }
}
