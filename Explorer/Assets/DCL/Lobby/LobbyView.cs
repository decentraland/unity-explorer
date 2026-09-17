using DCL.CharacterPreview;
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
        public Button ProfileMenuCloserButton { get; private set; } = null!;

        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;

        [field: SerializeField]
        public CharacterPreviewView CharacterPreviewView { get; private set; } = null!;

        [field: Header("Quick jump in")]
        [field: SerializeField]
        public TMP_Text WelcomeText { get; private set; } = null!;

        [field: SerializeField]
        public LobbyHomeCardView HomeCard { get; private set; } = null!;

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
        public LobbyPlacesCarousel RecommendedPlaces { get; private set; } = null!;
    }
}
