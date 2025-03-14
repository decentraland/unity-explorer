using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.FriendPanel.Sections.Requests
{
    public class RequestUserView : FriendPanelUserView
    {
        [field: SerializeField] public Button ContextMenuButton { get; private set; }
        [field: SerializeField] public Button DeleteButton { get; private set; }
        [field: SerializeField] public Button AcceptButton { get; private set; }
        [field: SerializeField] public TMP_Text RequestDateText { get; private set; }
        [field: SerializeField] public GameObject HasMessageIndicator { get; private set; }

        private DateTime requestDate;
        private FriendPanelStatus parentStatus;

        public DateTime RequestDate
        {
            get => requestDate;

            set
            {
                requestDate = value;
                RequestDateText.SetText(requestDate.ToString("MMM dd", CultureInfo.InvariantCulture).ToUpper());
            }
        }

        public FriendPanelStatus ParentStatus
        {
            get => parentStatus;

            set
            {
                parentStatus = value;
                if (value == FriendPanelStatus.SENT)
                    InhibitInteractionButtons();
            }
        }

        private void InhibitInteractionButtons()
        {
            buttons.Clear();
            buttons.Add(ContextMenuButton);
        }

        public override void Configure(FriendProfile profile)
        {
            buttons.Clear();
            buttons.Add(ContextMenuButton);
            buttons.Add(DeleteButton);
            buttons.Add(AcceptButton);
            base.Configure(profile);
        }

        protected override void ToggleButtonView(bool isActive)
        {
            base.ToggleButtonView(isActive);
            RequestDateText.gameObject.SetActive(!isActive);
        }
    }
}
