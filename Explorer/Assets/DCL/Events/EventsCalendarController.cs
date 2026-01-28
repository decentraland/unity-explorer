using System;

namespace DCL.Events
{
    public class EventsCalendarController : IDisposable
    {
        private readonly EventsCalendarView view;
        private readonly EventsController eventsController;

        public EventsCalendarController(
            EventsCalendarView view,
            EventsController eventsController)
        {
            this.view = view;
            this.eventsController = eventsController;

            eventsController.EventsClosed += UnloadEvents;
        }

        public void Dispose()
        {
            eventsController.EventsClosed -= UnloadEvents;
        }

        private void UnloadEvents()
        {

        }
    }
}
