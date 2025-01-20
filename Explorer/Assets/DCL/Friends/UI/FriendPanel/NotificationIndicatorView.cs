using TMPro;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel
{
    public class NotificationIndicatorView : MonoBehaviour
    {
        private const int MAX_NOTIFICATIONS_DISPLAYED = 9;

        [field: SerializeField] public TMP_Text NotificationText { get; private set; }

        public void SetNotificationCount(int count)
        {
            NotificationText.SetText(count > MAX_NOTIFICATIONS_DISPLAYED ? $"+{MAX_NOTIFICATIONS_DISPLAYED}" : $"{count}");
            NotificationText.gameObject.SetActive(count > 0);
        }
    }
}
