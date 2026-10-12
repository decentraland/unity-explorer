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
    ///     Lobby screen built with UI Toolkit. Its LobbyDocument.uxml hierarchy is reachable only while shown.
    ///     The avatar preview stays uGUI, in a canvas of the prefab that sorts just under the panel.
    /// </summary>
    public class LobbyDocumentView : PanelRendererViewBase
    {
        private const string AVATAR_HIT_AREA_NAME = "AvatarHitArea";
        private const string AVATAR_TOOLTIP_NAME = "AvatarTooltip";
        private const string AVATAR_DRAG_CURSOR_NAME = "AvatarDragCursor";
        private const string FRIEND_TOOLTIP_NAME = "FriendTooltip";
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

        public VisualTreeAsset PlaceCardTemplate => placeCardTemplate;

        public VisualTreeAsset FriendCardTemplate => friendCardTemplate;

        public VisualTreeAsset LiveEventCardTemplate => liveEventCardTemplate;

        public VisualTreeAsset UpcomingEventCardTemplate => upcomingEventCardTemplate;

        /// <summary>The same asset the Explore event cards use for their Share menu.</summary>
        public EventContextMenuConfiguration EventContextMenuSettings => eventContextMenuSettings;

        /// <summary>The uGUI avatar preview nested in the prefab, so it exists from the instantiation on.</summary>
        public CharacterPreviewView CharacterPreviewView => characterPreviewView;

        /// <summary>Transparent element over the figure taking its hover and click. Exists only while shown.</summary>
        public VisualElement AvatarHitArea => Element<VisualElement>(AVATAR_HIT_AREA_NAME);

        /// <summary>Hint beside the pointer over <see cref="AvatarHitArea" />. Exists only while shown.</summary>
        public VisualElement AvatarTooltip => Element<VisualElement>(AVATAR_TOOLTIP_NAME);

        /// <summary>Rotate cursor shown at the pointer while the figure is dragged. Exists only while shown.</summary>
        public VisualElement AvatarDragCursor => Element<VisualElement>(AVATAR_DRAG_CURSOR_NAME);

        /// <summary>Name of the friend picture under the pointer on a card. Exists only while shown.</summary>
        public VisualElement FriendTooltip => Element<VisualElement>(FRIEND_TOOLTIP_NAME);

        /// <summary>Credits widget of the top bar. It outlives the hierarchy, so its bindings stay.</summary>
        public CreditsPanelElement Credits => credits ??= new CreditsPanelElement();

        /// <summary>Profile widget of the top bar. It outlives the hierarchy like <see cref="Credits" />.</summary>
        public ProfileWidgetElement Profile => profile ??= new ProfileWidgetElement();

        /// <summary>Exists only while the view is shown.</summary>
        public Button NotificationsButton => Element<Button>(NOTIFICATIONS_NAME);

        /// <summary>Unread count over <see cref="NotificationsButton" />. Exists only while shown.</summary>
        public Label UnreadBadge => Element<Label>(UNREAD_BADGE_NAME);

        /// <summary>Exists only while the view is shown.</summary>
        public Button CloseButton => Element<Button>(CLOSE_NAME);

        /// <summary>Exists only while the view is shown.</summary>
        public Label WelcomeText => Element<Label>(WELCOME_TEXT_NAME);

        /// <summary>Hero card of the place the session lands in. Exists only while the view is shown.</summary>
        public LobbyLandingCardElement LandingCard => Element<LobbyLandingCardElement>(LANDING_CARD_NAME);

        /// <summary>Section of the recently visited places: title and row of cards. Exists only while shown.</summary>
        public VisualElement RecentPlaces => Section(RECENT_PLACES_NAME);

        /// <summary>Section of the featured places, title and rail. Exists only while the view is shown.</summary>
        public VisualElement FeaturedPlaces => Section(FEATURED_PLACES_NAME);

        /// <summary>Section of the online friends, header and rail. Exists only while the view is shown.</summary>
        public VisualElement Friends => Section(FRIENDS_NAME);

        /// <summary>Section of the events, title and the two rows below. Exists only while the view is shown.</summary>
        public VisualElement Events => Section(EVENTS_NAME);

        /// <summary>Row of the live events inside <see cref="Events" />. Exists only while the view is shown.</summary>
        public VisualElement LiveEvents => Section(LIVE_EVENTS_NAME);

        /// <summary>Row of the upcoming events inside <see cref="Events" />. Exists only while shown.</summary>
        public VisualElement UpcomingEvents => Section(UPCOMING_EVENTS_NAME);

        /// <summary>
        ///     Moves <see cref="Credits" /> and <see cref="Profile" /> into their slots. A no-op while the renderer
        ///     keeps the hierarchy, which it rebuilds only for a changed LobbyDocument.uxml.
        /// </summary>
        public void AttachTopBarWidgets()
        {
            Section(CREDITS_SLOT_NAME).Add(Credits);
            Section(PROFILE_SLOT_NAME).Add(Profile);
        }

        private VisualElement Section(string elementName) =>
            Element<VisualElement>(elementName);

        private T Element<T>(string elementName) where T: VisualElement =>
            root?.Q<T>(elementName) ?? throw new InvalidOperationException($"The lobby hierarchy exists only while the view is shown, {elementName} is not available");
    }
}
