using DCL.Profiles;
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.Sections.Requests
{
    public class RequestUserView : FriendPanelUserView
    {
        [field: SerializeField] public Button ContextMenuButton { get; private set; }
        [field: SerializeField] public TMP_Text RequestDateText { get; private set; }
        [field: SerializeField] public TMP_Text MessagePreviewText { get; private set; }

        private DateTime requestDate;
        public DateTime RequestDate
        {
            get => requestDate;

            set
            {
                requestDate = value;
                RequestDateText.SetText(requestDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture));
            }
        }

        public override void Configure(Profile profile)
        {
            buttons = new[] { ContextMenuButton };
            base.Configure(profile);
        }

        protected override void ToggleButtonView(bool isActive)
        {
            base.ToggleButtonView(isActive);
            RequestDateText.gameObject.SetActive(!isActive);
        }
    }
}