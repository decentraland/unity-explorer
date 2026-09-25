using DCL.CharacterPreview;
using DCL.Notifications.NotificationsMenu;
using DCL.UI.Credits;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Lobby
{
    public class LobbyView : ViewBase, IView
    {
        [field: Header("Top Bar")]
        [field: SerializeField]
        public CreditsPanelView CreditsPanelView { get; private set; } = null!;

        [field: SerializeField]
        public ProfileWidgetView ProfileWidgetView { get; private set; } = null!;

        [field: SerializeField]
        public ProfileMenuView ProfileMenuView { get; private set; } = null!;

        [field: SerializeField]
        public Button NotificationsButton { get; private set; } = null!;

        [field: SerializeField]
        public NotificationsMenuView NotificationsMenuView { get; private set; } = null!;

        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;

        [field: SerializeField]
        public CharacterPreviewView CharacterPreviewView { get; private set; } = null!;

        /// <summary>
        ///     Sits over the avatar inside the full-screen preview, so only the figure itself is hoverable and clickable.
        ///     Its tooltip and cursor swap are wired in the prefab.
        /// </summary>
        [field: SerializeField]
        public Button AvatarButton { get; private set; } = null!;

        [field: Header("Quick jump in")]
        [field: SerializeField]
        public TMP_Text WelcomeText { get; private set; } = null!;

        [field: SerializeField]
        public LobbyLandingCardView LandingCard { get; private set; } = null!;

        [field: Header("Jump back in")]
        [field: SerializeField]
        public GameObject RecentPlacesSection { get; private set; } = null!;

        /// <summary>
        ///     Laid out in the prefab; the number of cards caps how many recent places are shown.
        /// </summary>
        [field: SerializeField]
        public LobbyPlaceCardView[] RecentPlaceCards { get; private set; } = null!;

        [field: Header("Recommended places")]
        [field: SerializeField]
        public GameObject RecommendedPlacesSection { get; private set; } = null!;

        [field: SerializeField]
        public LobbyCarouselView RecommendedPlaces { get; private set; } = null!;

        [field: Header("Events")]
        [field: SerializeField]
        public GameObject EventsSection { get; private set; } = null!;

        /// <summary>
        ///     Hidden while nothing is live; the upcoming carousel moves up to take its place.
        /// </summary>
        [field: SerializeField]
        public GameObject LiveEventsSection { get; private set; } = null!;

        [field: SerializeField]
        public LobbyCarouselView LiveEvents { get; private set; } = null!;

        [field: SerializeField]
        public GameObject UpcomingEventsSection { get; private set; } = null!;

        [field: SerializeField]
        public LobbyEventRailView UpcomingEvents { get; private set; } = null!;

        [field: Header("Friends")]
        [field: SerializeField]
        public LobbyFriendsSectionView FriendsSection { get; private set; } = null!;
    }
}
