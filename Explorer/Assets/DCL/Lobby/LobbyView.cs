using DCL.CharacterPreview;
using DCL.UI.Credits;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Lobby
{
    public class LobbyView : ViewBase, IView
    {
        [field: SerializeField]
        public Button JumpInButton { get; private set; } = null!;

        [field: Space(10)]
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

        [field: Header("Jump back in")]
        [field: SerializeField]
        public GameObject RecentPlacesSection { get; private set; } = null!;

        /// <summary>
        ///     Laid out in the prefab; the number of cards caps how many recent places are shown.
        /// </summary>
        [field: SerializeField]
        public LobbyPlaceCardView[] RecentPlaceCards { get; private set; } = null!;
    }
}
