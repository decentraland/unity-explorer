using DCL.Notifications.NotificationsMenu;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     Canvas of the uGUI popups the lobby opens from its top bar; they cannot hang from a UI Toolkit panel.
    /// </summary>
    public class LobbyPopupsView : MonoBehaviour
    {
        [field: SerializeField]
        public NotificationsMenuView NotificationsMenuView { get; private set; } = null!;
    }
}
