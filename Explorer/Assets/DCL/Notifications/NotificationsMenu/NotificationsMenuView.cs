using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Notifications.NotificationsMenu
{
    public class NotificationsMenuView : MonoBehaviour
    {
        [field: SerializeField]
        public LoopListView2 LoopList { get; private set; }

        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;
    }
}
