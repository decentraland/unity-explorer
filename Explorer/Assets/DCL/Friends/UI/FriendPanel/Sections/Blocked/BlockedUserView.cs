using DCL.Friends.UI.FriendPanel.Sections.Friends;
using DCL.UI.Profiles.Helpers;
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
                BlockedDateText.SetText(FriendListSectionUtilities.FormatDate(blockedDate));
            }
        }

        public override void Configure(FriendProfile profile, ProfileRepositoryWrapper profileDataProvider)
        {
            buttons.Clear();
            buttons.Add(UnblockButton);
            buttons.Add(ContextMenuButton);
            base.Configure(profile, profileDataProvider);
        }

        protected override void ToggleButtonView(bool isActive)
        {
            base.ToggleButtonView(isActive);
            BlockedDateText.gameObject.SetActive(!isActive);
        }
    }
}
