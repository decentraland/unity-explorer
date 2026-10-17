using DCL.EventsApi;
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Communities.EventInfo
{
    public class EventScheduleRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<IEventDTO, DateTime>? AddToCalendarClicked;

        [SerializeField] private GameObject background = null!;
        [SerializeField] private TMP_Text scheduleText = null!;
        [SerializeField] private Button addToCalendarButton = null!;

        private IEventDTO currentEventInfo = null!;
        private DateTime currentUtcDateTime;

        private readonly StringBuilder eventSchedulesStringBuilder = new ();

        private void Awake() =>
            addToCalendarButton.onClick.AddListener(() => AddToCalendarClicked?.Invoke(currentEventInfo, currentUtcDateTime));

        private void OnEnable() =>
            background.SetActive(false);

        private void OnDestroy() =>
            addToCalendarButton.onClick.RemoveAllListeners();

        public void Configure(IEventDTO eventInfo, DateTime utcStart)
        {
            EventUtilities.FormatEventString(utcStart, eventInfo.Duration, eventSchedulesStringBuilder);

            scheduleText.text = eventSchedulesStringBuilder.ToString();
            currentEventInfo = eventInfo;
            currentUtcDateTime = utcStart;

            eventSchedulesStringBuilder.Clear();
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            background.SetActive(true);

        public void OnPointerExit(PointerEventData eventData) =>
            background.SetActive(false);
    }
}
