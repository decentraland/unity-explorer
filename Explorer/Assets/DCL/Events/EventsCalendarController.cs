using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Utility;

namespace DCL.Events
{
    public class EventsCalendarController : IDisposable
    {
        private readonly EventsCalendarView view;
        private readonly EventsController eventsController;

        private CancellationTokenSource? loadEventsCts;

        public EventsCalendarController(
            EventsCalendarView view,
            EventsController eventsController)
        {
            this.view = view;
            this.eventsController = eventsController;

            view.InitializeEventsLists();

            eventsController.EventsOpen += OnSectionOpened;
            eventsController.EventsClosed += OnSectionClosed;
        }

        public void Dispose()
        {
            eventsController.EventsOpen -= OnSectionOpened;
            eventsController.EventsClosed -= OnSectionClosed;

            loadEventsCts?.SafeCancelAndDispose();
        }

        private void OnSectionOpened() =>
            LoadEvents();

        private void OnSectionClosed() =>
            UnloadEvents();

        private void LoadEvents()
        {
            loadEventsCts = loadEventsCts.SafeRestart();
            LoadEventsAsync(loadEventsCts.Token).Forget();
        }

        private async UniTask LoadEventsAsync(CancellationToken ct)
        {
            view.ClearAllEvents();
            view.SetAsLoading(true);

            await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: ct);

            view.SetEvents(new [] { "Test 1.1", "Test 1.2", "Test 1.3", "Test 1.4", "Test 1.5" }, 0, true);
            view.SetEvents(new [] { "Test 2.1", "Test 2.2", "Test 2.3", "Test 2.4", "Test 2.5", "Test 2.6", "Test 2.7", "Test 2.8" }, 1, true);
            view.SetEvents(new [] { "Test 3.1", "Test 3.2", "Test 3.3" }, 2, true);
            view.SetEvents(new [] { "Test 4.1", "Test 4.2", "Test 4.3", "Test 4.4" }, 3, true);
            view.SetEvents(new [] { "Test 5.1", "Test 5.2", "Test 5.3", "Test 5.4", "Test 5.5", "Test 5.6", "Test 5.7", "Test 5.8", "Test 5.9", "Test 5.10" }, 4, true);

            view.SetAsLoading(false);
        }

        private void UnloadEvents() =>
            view.ClearAllEvents();
    }
}
