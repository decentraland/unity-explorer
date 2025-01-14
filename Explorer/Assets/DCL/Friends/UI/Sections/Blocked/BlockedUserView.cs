using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.Sections.Blocked
{
    public class BlockedUserView : FriendPanelUserView
    {
        [field: SerializeField] public Button UnblockButton { get; private set; }
        [field: SerializeField] public Button ContextMenuButton { get; private set; }
        [field: SerializeField] public TMP_Text BlockedDateText { get; private set; }

        public DateTime BlockedDate { get; private set; }

        private void Start()
        {
            buttons = new[] { UnblockButton, ContextMenuButton };
        }

        public void Configure(string userWalletAddress, DateTime blockedDate)
        {
            UserWalletAddress = userWalletAddress;
            BlockedDate = blockedDate;

            BlockedDateText.SetText($"{blockedDate:MM/dd}");
        }

        protected override void ToggleButtonView(bool isActive)
        {
            base.ToggleButtonView(isActive);
            BlockedDateText.gameObject.SetActive(!isActive);
        }
    }
}
