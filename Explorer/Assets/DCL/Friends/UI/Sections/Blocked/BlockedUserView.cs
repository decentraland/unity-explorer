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

        private void Start()
        {
            buttons = new[] { UnblockButton, ContextMenuButton };
        }

        protected override void ToggleButtonView(bool isActive)
        {
            base.ToggleButtonView(isActive);
            BlockedDateText.gameObject.SetActive(!isActive);
        }
    }
}
