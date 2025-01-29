using DCL.WebRequests;
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
        public DateTime RequestDate
        {
            get => requestDate;

            set
            {
                requestDate = value;
                RequestDateText.SetText(requestDate.ToString("MMM dd", CultureInfo.InvariantCulture).ToUpper());
            }
        }

        public void InhibitInteractionButtons()
        {
            buttons = new [] { ContextMenuButton };
        }

        public override void Configure(FriendProfile profile, IWebRequestController webRequestController, IProfileThumbnailCache profileThumbnailCache)
        {
            buttons = new[] { ContextMenuButton, DeleteButton, AcceptButton };
            base.Configure(profile, webRequestController, profileThumbnailCache);
        }

        protected override void ToggleButtonView(bool isActive)
        {
            base.ToggleButtonView(isActive);
            RequestDateText.gameObject.SetActive(!isActive);
        }
    }
}
