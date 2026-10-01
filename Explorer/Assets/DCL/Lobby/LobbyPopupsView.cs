using DCL.Notifications.NotificationsMenu;
using DCL.UI.Profiles;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     Canvas of the popups the lobby opens from its top bar, laid out under the widgets that open them. The lobby is a UI Toolkit
    ///     panel while the popups are uGUI views, so they hang from this canvas rather than from the lobby.
    /// </summary>
    public class LobbyPopupsView : MonoBehaviour
    {
        [field: SerializeField]
        public ProfileMenuView ProfileMenuView { get; private set; } = null!;

        [field: SerializeField]
        public NotificationsMenuView NotificationsMenuView { get; private set; } = null!;
    }
}
