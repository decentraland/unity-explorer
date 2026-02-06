using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Events
{
    public class EventsDaySelectorButton : MonoBehaviour
    {
        private const string TODAY_TEXT = "Today";
        private const string TOMORROW_TEXT = "Tomorrow";

        public event Action<DateTime>? ButtonClicked;

        [SerializeField] private Button mainButton = null!;
        [SerializeField] private TMP_Text dayText = null!;
        [SerializeField] private Image backgroundImage = null!;
        [SerializeField] private Color todayTextColor;
        [SerializeField] private Color nonTodayTextColor;

        private DateTime currentDate;

        private void Awake() =>
            mainButton.onClick.AddListener(() => ButtonClicked?.Invoke(currentDate));

        private void OnDestroy() =>
            mainButton.onClick.RemoveAllListeners();

        public void Setup(DateTime date)
        {
            currentDate = date;

            var today = DateTime.Today;

            if (date.Date == today)
                dayText.text = TODAY_TEXT;
            else if (date.Date == today.AddDays(1))
                dayText.text = TOMORROW_TEXT;
            else
                dayText.text = date.ToString("ddd, MMM dd", CultureInfo.InvariantCulture);

            backgroundImage.color = new Color(backgroundImage.color.r, backgroundImage.color.g, backgroundImage.color.b, date.Date == today ? 1f : 0f);
            dayText.color = date.Date == today ? todayTextColor : nonTodayTextColor;
        }
    }
}
