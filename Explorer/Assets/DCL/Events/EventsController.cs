using DCL.Input;
using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Events
{
    public class EventsController : ISection, IDisposable
    {
        public event Action? EventsOpen;
        public event Action? EventsClosed;

        private readonly EventsView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;

        private bool isSectionActivated;
        private readonly EventsCalendarController eventsCalendarController;

        public EventsController(
            EventsView view,
            ICursor cursor)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;

            eventsCalendarController = new EventsCalendarController(view.EventsCalendarView, this);
        }

        public void Dispose() =>
            eventsCalendarController.Dispose();

        public void Activate()
        {
            if (isSectionActivated)
                return;

            isSectionActivated = true;
            view.SetViewActive(true);
            cursor.Unlock();
            EventsOpen?.Invoke();
        }

        public void Deactivate()
        {
            isSectionActivated = false;
            view.SetViewActive(false);
            EventsClosed?.Invoke();
        }

        public void Animate(int triggerId) =>
            view.PlayAnimator(triggerId);

        public void ResetAnimator() =>
            view.ResetAnimator();

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
