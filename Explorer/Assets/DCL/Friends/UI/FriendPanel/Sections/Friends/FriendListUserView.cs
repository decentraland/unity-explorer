using DCL.WebRequests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public class FriendListUserView : FriendPanelUserView
    {
        [field: SerializeField] public Button ContextMenuButton { get; private set; }
        [field: SerializeField] public Button JumpInButton { get; private set; }

        [field: Header("Online status")]
        [field: SerializeField] public GameObject OnlineStatusContainer { get; private set; }
        [field: SerializeField] public TMP_Text OnlineStatusText { get; private set; }
        [field: SerializeField] public GameObject OnlineStatusIndicator { get; private set; }
        [field: SerializeField] public Image OnlineStatusColorIndicator { get; private set; }
        [field: SerializeField] public OnlineStatusConfiguration OnlineStatusConfiguration { get; private set; }

        public override void Configure(FriendProfile profile, IWebRequestController webRequestController, IProfileThumbnailCache profileThumbnailCache)
        {
            buttons.Clear();
            buttons.Add(JumpInButton);
            buttons.Add(ContextMenuButton);
            base.Configure(profile, webRequestController, profileThumbnailCache);
            SetOnlineStatus(OnlineStatus.OFFLINE);
        }

        public void SetOnlineStatus(OnlineStatus onlineStatus)
        {
            OnlineStatusConfigurationData configurationData = OnlineStatusConfiguration.GetConfiguration(onlineStatus);
            OnlineStatusText.SetText(configurationData.StatusText);
            OnlineStatusColorIndicator.color = configurationData.StatusColor;

            buttons.Clear();
            buttons.Add(ContextMenuButton);

            if (onlineStatus != OnlineStatus.OFFLINE)
                buttons.Add(JumpInButton);
        }

        public void ToggleOnlineStatus(bool isActive)
        {
            OnlineStatusContainer.SetActive(isActive);
            OnlineStatusIndicator.SetActive(isActive);

            if (!isActive)
            {
                buttons.Clear();
                buttons.Add(ContextMenuButton);
            }
        }
    }
}
