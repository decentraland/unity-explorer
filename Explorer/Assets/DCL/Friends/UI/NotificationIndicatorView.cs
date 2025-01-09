using TMPro;
using UnityEngine;

namespace DCL.Friends.UI
{
    public class NotificationIndicatorView : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text NotificationText { get; private set; }

        public void SetNotificationCount(int count)
        {
            NotificationText.SetText($"{count}");
            NotificationText.gameObject.SetActive(count > 0);
        }
    }
}
