using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Events
{
    public class EventsView : MonoBehaviour
    {
        public event Action? CreateButtonClicked;

        [Header("Buttons")]
        [SerializeField] private Button createEventButton = null!;

        [Header("Views")]
        [SerializeField] private EventsCalendarView eventsCalendarView = null!;
        [SerializeField] private EventsByDayView eventsByDayView = null!;

        [Header("Animators")]
        [SerializeField] private Animator panelAnimator = null!;
        [SerializeField] private Animator headerAnimator = null!;

        public EventsCalendarView EventsCalendarView => eventsCalendarView;
        public EventsByDayView EventsByDayView => eventsByDayView;

        private void Awake() =>
            createEventButton.onClick.AddListener(() => CreateButtonClicked?.Invoke());

        private void OnDestroy() =>
            createEventButton.onClick.RemoveAllListeners();

        public void SetViewActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }

        public void PlayAnimator(int triggerId)
        {
            panelAnimator.SetTrigger(triggerId);
            headerAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            panelAnimator.Rebind();
            headerAnimator.Rebind();
            panelAnimator.Update(0);
            headerAnimator.Update(0);
        }

        public void OpenSection(EventsSection section)
        {
            eventsCalendarView.gameObject.SetActive(section == EventsSection.CALENDAR);
            eventsByDayView.gameObject.SetActive(section == EventsSection.EVENTS_BY_DAY);
        }
    }
}
