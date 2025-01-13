using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI
{
    public enum FriendPanelStatus
    {
        ONLINE,
        OFFLINE,
        RECEIVED,
        SENT,
    }

    public class StatusWrapperView : MonoBehaviour
    {
        [field: SerializeField] public Button FolderButton { get; private set; }
        [field: SerializeField] public Image FolderButtonGraphics { get; private set; }
        [field: SerializeField] public TMP_Text StatusText { get; private set; }

        public void SetStatusText(FriendPanelStatus status, int amount)
        {
            string statusText = status switch
            {
                FriendPanelStatus.ONLINE => "ONLINE",
                FriendPanelStatus.OFFLINE => "OFFLINE",
                FriendPanelStatus.RECEIVED => "RECEIVED",
                FriendPanelStatus.SENT => "SENT",
                _ => "Unknown"
            };

            StatusText.SetText($"{statusText} ({amount})");
        }
    }
}
