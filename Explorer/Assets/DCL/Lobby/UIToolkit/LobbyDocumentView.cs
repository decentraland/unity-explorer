using DCL.CharacterPreview;
using DCL.Communities.EventInfo;
using DCL.UI.Credits;
using DCL.UI.ProfileElements;
using MVC;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Lobby screen built with UI Toolkit. Its hierarchy lives in LobbyDocument.uxml and is reachable only while the view is shown.
    ///     The avatar preview stays uGUI: the prefab carries it in a canvas that sorts just under the panel, so the stage it renders
    ///     is the background and the document draws over it without painting anything behind the figure.
    /// </summary>
    public class LobbyDocumentView : PanelRendererViewBase
    {
        private const string AVATAR_HIT_AREA_NAME = "AvatarHitArea";
        private const string AVATAR_TOOLTIP_NAME = "AvatarTooltip";
        private const string WELCOME_TEXT_NAME = "WelcomeText";
        private const string LANDING_CARD_NAME = "LandingCard";
        private const string RECENT_PLACES_NAME = "RecentPlaces";
        private const string FEATURED_PLACES_NAME = "FeaturedPlaces";
        private const string FRIENDS_NAME = "Friends";
        private const string EVENTS_NAME = "Events";
        private const string LIVE_EVENTS_NAME = "LiveEvents";
        private const string UPCOMING_EVENTS_NAME = "UpcomingEvents";
        private const string CREDITS_SLOT_NAME = "CreditsSlot";
        private const string PROFILE_SLOT_NAME = "ProfileSlot";
        private const string NOTIFICATIONS_NAME = "Notifications";
        private const string UNREAD_BADGE_NAME = "UnreadBadge";
        private const string CLOSE_NAME = "Close";

        [SerializeField] private VisualTreeAsset placeCardTemplate = null!;
        [SerializeField] private VisualTreeAsset friendCardTemplate = null!;
        [SerializeField] private VisualTreeAsset liveEventCardTemplate = null!;
        [SerializeField] private VisualTreeAsset upcomingEventCardTemplate = null!;
        [SerializeField] private EventContextMenuConfiguration eventContextMenuSettings = null!;
        [SerializeField] private CharacterPreviewView characterPreviewView = null!;

        private CreditsPanelElement? credits;
        private ProfileWidgetElement? profile;

        /// <summary>
        ///     LobbyPlaceCard.uxml, cloned once per card of the place rows.
        /// </summary>
        public VisualTreeAsset PlaceCardTemplate => placeCardTemplate;

        /// <summary>
        ///     LobbyFriendCard.uxml, cloned once per card of the friends row.
        /// </summary>
        public VisualTreeAsset FriendCardTemplate => friendCardTemplate;

        /// <summary>
        ///     LobbyLiveEventCard.uxml, cloned once per card of the live events row.
        /// </summary>
        public VisualTreeAsset LiveEventCardTemplate => liveEventCardTemplate;

        /// <summary>
        ///     LobbyUpcomingEventCard.uxml, cloned once per card of the upcoming events row.
        /// </summary>
        public VisualTreeAsset UpcomingEventCardTemplate => upcomingEventCardTemplate;

        /// <summary>
        ///     Texts, icons and layout of the menu the Share button of an upcoming event card opens, the same asset the Explore cards use.
        /// </summary>
        public EventContextMenuConfiguration EventContextMenuSettings => eventContextMenuSettings;

        /// <summary>
        ///     The uGUI avatar preview nested in the prefab. Like the widgets it survives the hide.
        /// </summary>
        public CharacterPreviewView CharacterPreviewView => characterPreviewView;

        /// <summary>
        ///     Transparent element over the figure that catches its hover and click; the preview canvas itself takes no input.
        ///     Exists only while the view is shown; whether it is displayed is up to the controller.
        /// </summary>
        public VisualElement AvatarHitArea => Element<VisualElement>(AVATAR_HIT_AREA_NAME);

        /// <summary>
        ///     Hint that follows the pointer while it is over <see cref="AvatarHitArea" />; the controller places and displays it.
        ///     Exists only while the view is shown.
        /// </summary>
        public VisualElement AvatarTooltip => Element<VisualElement>(AVATAR_TOOLTIP_NAME);

        /// <summary>
        ///     Credits widget of the top bar. It outlives the hierarchy, so what is bound to it stays bound;
        ///     <see cref="AttachTopBarWidgets" /> puts it back into each new hierarchy.
        /// </summary>
        public CreditsPanelElement Credits => credits ??= new CreditsPanelElement();

        /// <summary>
        ///     Profile widget of the top bar. It survives the hide like <see cref="Credits" />.
        /// </summary>
        public ProfileWidgetElement Profile => profile ??= new ProfileWidgetElement();

        /// <summary>
        ///     Button of the top bar that opens the notifications. Exists only while the view is shown.
        /// </summary>
        public Button NotificationsButton => Element<Button>(NOTIFICATIONS_NAME);

        /// <summary>
        ///     Unread count over the corner of <see cref="NotificationsButton" />. Exists only while the view is shown.
        /// </summary>
        public Label UnreadBadge => Element<Label>(UNREAD_BADGE_NAME);

        /// <summary>
        ///     Button of the top bar that closes the lobby. Exists only while the view is shown.
        /// </summary>
        public Button CloseButton => Element<Button>(CLOSE_NAME);

        /// <summary>
        ///     Greeting over the landing card. Exists only while the view is shown.
        /// </summary>
        public Label WelcomeText => Element<Label>(WELCOME_TEXT_NAME);

        /// <summary>
        ///     Hero card of the place the session lands in. Exists only while the view is shown.
        /// </summary>
        public LobbyLandingCardElement LandingCard => Element<LobbyLandingCardElement>(LANDING_CARD_NAME);

        /// <summary>
        ///     Section of the most recently visited places, with its title and its row of cards. Exists only while the view is shown.
        /// </summary>
        public VisualElement RecentPlaces => Section(RECENT_PLACES_NAME);

        /// <summary>
        ///     Section of the featured places, with its title and its rail. Exists only while the view is shown.
        /// </summary>
        public VisualElement FeaturedPlaces => Section(FEATURED_PLACES_NAME);

        /// <summary>
        ///     Section of the online friends, with its header and its rail. Exists only while the view is shown.
        /// </summary>
        public VisualElement Friends => Section(FRIENDS_NAME);

        /// <summary>
        ///     Section of the events, with its title and the two rows below. Exists only while the view is shown.
        /// </summary>
        public VisualElement Events => Section(EVENTS_NAME);

        /// <summary>
        ///     Row of the live events inside <see cref="Events" />, with its rail. Exists only while the view is shown.
        /// </summary>
        public VisualElement LiveEvents => Section(LIVE_EVENTS_NAME);

        /// <summary>
        ///     Row of the upcoming events inside <see cref="Events" />, with its rail. Exists only while the view is shown.
        /// </summary>
        public VisualElement UpcomingEvents => Section(UPCOMING_EVENTS_NAME);

        /// <summary>
        ///     Moves <see cref="Credits" /> and <see cref="Profile" /> into their slots of the current hierarchy. The renderer keeps
        ///     the hierarchy across hides, so this is a no-op until it rebuilds one for a changed LobbyDocument.uxml.
        /// </summary>
        public void AttachTopBarWidgets()
        {
            Section(CREDITS_SLOT_NAME).Add(Credits);
            Section(PROFILE_SLOT_NAME).Add(Profile);
        }

        private VisualElement Section(string name) =>
            Element<VisualElement>(name);

        private T Element<T>(string name) where T: VisualElement =>
            Root?.Q<T>(name) ?? throw new InvalidOperationException($"The lobby hierarchy exists only while the view is shown, {name} is not available");
    }
}
