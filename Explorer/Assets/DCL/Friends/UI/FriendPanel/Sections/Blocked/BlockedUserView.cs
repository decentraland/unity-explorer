using DCL.WebRequests;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.FriendPanel.Sections.Blocked
{
    public class BlockedUserView : FriendPanelUserView
    {
        [field: SerializeField] public Button UnblockButton { get; private set; }
        [field: SerializeField] public Button ContextMenuButton { get; private set; }
        [field: SerializeField] public TMP_Text BlockedDateText { get; private set; }

        private DateTime blockedDate;
        public DateTime BlockedDate
        {
            get => blockedDate;

            set
            {
                blockedDate = value;
                BlockedDateText.SetText($"{blockedDate:MM/dd}");
            }
        }

        public override void Configure(FriendProfile profile, IWebRequestController webRequestController, IProfileThumbnailCache profileThumbnailCache)
        {
            buttons = new[] { UnblockButton, ContextMenuButton };
            base.Configure(profile, webRequestController, profileThumbnailCache);
        }

        protected override void ToggleButtonView(bool isActive)
        {
            base.ToggleButtonView(isActive);
            BlockedDateText.gameObject.SetActive(!isActive);
        }
    }
}
