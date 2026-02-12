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

        [SerializeField] private Button todayButton = null!;
        [SerializeField] private Button nonTodayButton = null!;
        [SerializeField] private TMP_Text todayText = null!;
        [SerializeField] private TMP_Text nonTodayText = null!;

        private DateTime currentDate;

        private void Awake()
        {
            todayButton.onClick.AddListener(() => ButtonClicked?.Invoke(currentDate));
            nonTodayButton.onClick.AddListener(() => ButtonClicked?.Invoke(currentDate));
        }

        private void OnDestroy()
        {
            todayButton.onClick.RemoveAllListeners();
            nonTodayButton.onClick.RemoveAllListeners();
        }

        public void Setup(DateTime date)
        {
            currentDate = date;

            var today = DateTime.Today;

            if (date.Date == today)
                todayText.text = TODAY_TEXT;
            else if (date.Date == today.AddDays(1))
                nonTodayText.text = TOMORROW_TEXT;
            else
                nonTodayText.text = date.ToString("ddd, MMM dd", CultureInfo.InvariantCulture);

            todayButton.gameObject.SetActive(date.Date == today);
            nonTodayButton.gameObject.SetActive(date.Date != today);
        }
    }
}
