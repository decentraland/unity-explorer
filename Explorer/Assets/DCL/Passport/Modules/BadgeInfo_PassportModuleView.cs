using DCL.BadgesAPIService;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class BadgeInfo_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject MainContainer { get; private set; }

        [field: SerializeField]
        public GameObject MainLoadingSpinner { get; private set; }

        [field: SerializeField]
        public Image Badge2DImage { get; private set; }

        [field: SerializeField]
        public TMP_Text BadgeNameText { get; private set; }

        [field: SerializeField]
        public TMP_Text BadgeDateText { get; private set; }

        [field: SerializeField]
        public TMP_Text BadgeDescriptionText { get; private set; }

        public void Setup(BadgeInfo badgeInfo)
        {
            //Badge2DImage.sprite = null;
            BadgeNameText.text = badgeInfo.name;
            BadgeDateText.text = !badgeInfo.isLocked ? FormatTimestampDate(badgeInfo.awarded_at) : "--";
            BadgeDescriptionText.text = badgeInfo.description;
        }

        public void SetAsLoading(bool isLoading)
        {
            MainLoadingSpinner.SetActive(isLoading);
            MainContainer.SetActive(!isLoading);
        }

        private static string FormatTimestampDate(string timestampString)
        {
            DateTime date = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(timestampString)).DateTime;
            var formattedDate = date.ToString("MMM. yyyy", System.Globalization.CultureInfo.InvariantCulture);
            return formattedDate;
        }
    }
}
