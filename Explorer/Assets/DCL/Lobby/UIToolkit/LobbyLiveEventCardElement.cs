using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     UI Toolkit counterpart of <see cref="LobbyLiveEventCardView" />: full-bleed thumbnail with the Live badge and how many people
    ///     are attending on top, plus the name and the host over the gradient at the bottom. Its hierarchy comes from LobbyLiveEventCard.uxml,
    ///     so the children only exist once that template is instantiated; the badges and the gradient are laid out by LobbyLiveEventCard.uss.
    /// </summary>
    [UxmlElement]
    public partial class LobbyLiveEventCardElement : VisualElement, ILobbyThumbnailCard
    {
        private const string USS_BLOCK = "lobby-live-event-card";
        private const string USS_LOADING = USS_BLOCK + "--loading";

        private const string TITLE_NAME = "Title";
        private const string HOST_NAME = "Host";
        private const string ATTENDEES_NAME = "Attendees";
        private const string THUMBNAIL_NAME = "Thumbnail";

        /// <summary>
        ///     Raised on a click anywhere on the card.
        /// </summary>
        public Action? Clicked;

        private Label? titleLabel;
        private Label? hostLabel;
        private Label? attendeesLabel;
        private VisualElement? thumbnail;

        // Attribute values and the thumbnail can arrive before the template children do; they are applied once those attach
        private string title = string.Empty;
        private string host = string.Empty;
        private int attendees;
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

        /// <summary>
        ///     Null falls back to the default event thumbnail of the stylesheet.
        /// </summary>
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

        public LobbyLiveEventCardElement()
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
            attendeesLabel = this.Q<Label>(ATTENDEES_NAME);
            thumbnail = this.Q<VisualElement>(THUMBNAIL_NAME);

            titleLabel.text = title;
            hostLabel.text = host;
            attendeesLabel.text = attendees.ToString();
            ApplyThumbnail();
        }

        private void ApplyThumbnail()
        {
            thumbnail!.style.backgroundImage = thumbnailSprite == null ? StyleKeyword.Null : new StyleBackground(thumbnailSprite);
        }

        private void OnClicked() =>
            Clicked?.Invoke();
    }
}
