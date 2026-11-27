using Cysharp.Threading.Tasks;
using DCL.Minimap;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    /// <summary>
    ///     Reports how the minimap is used. Realm and parcel are attached to every event by the common traits,
    ///     so only the action-specific values travel in the payload.
    /// </summary>
    public class MinimapAnalytics : IDisposable
    {
        private const int SKYBOX_TIME_DEBOUNCE_MS = 1000;

        private readonly IAnalyticsController analytics;
        private readonly int skyboxTimeDebounceMs;
        private readonly EventSubscriptionScope scope = new ();

        private CancellationTokenSource? skyboxTimeCts;

        public MinimapAnalytics(IAnalyticsController analytics, IEventBus eventBus, int skyboxTimeDebounceMs = SKYBOX_TIME_DEBOUNCE_MS)
        {
            this.analytics = analytics;
            this.skyboxTimeDebounceMs = skyboxTimeDebounceMs;

            scope.Add(eventBus.Subscribe<MinimapEvents.Expanded>(OnExpanded));
            scope.Add(eventBus.Subscribe<MinimapEvents.Collapsed>(OnCollapsed));
            scope.Add(eventBus.Subscribe<MinimapEvents.MapClicked>(OnMapClicked));
            scope.Add(eventBus.Subscribe<MinimapEvents.ContextMenuOpened>(OnContextMenuOpened));
            scope.Add(eventBus.Subscribe<MinimapEvents.FavoriteClicked>(OnFavoriteClicked));
            scope.Add(eventBus.Subscribe<MinimapEvents.SkyboxWidgetToggled>(OnSkyboxWidgetToggled));
            scope.Add(eventBus.Subscribe<MinimapEvents.SkyboxTimeChanged>(OnSkyboxTimeChanged));
            scope.Add(eventBus.Subscribe<MinimapEvents.SkyboxDayCycleToggled>(OnSkyboxDayCycleToggled));
        }

        public void Dispose()
        {
            scope.Dispose();
            skyboxTimeCts.SafeCancelAndDispose();
        }

        private void OnExpanded(MinimapEvents.Expanded _) =>
            analytics.Track(AnalyticsEvents.Minimap.EXPANDED);

        private void OnCollapsed(MinimapEvents.Collapsed _) =>
            analytics.Track(AnalyticsEvents.Minimap.COLLAPSED);

        private void OnMapClicked(MinimapEvents.MapClicked _) =>
            analytics.Track(AnalyticsEvents.Minimap.MAP_CLICKED);

        private void OnContextMenuOpened(MinimapEvents.ContextMenuOpened _) =>
            analytics.Track(AnalyticsEvents.Minimap.CONTEXT_MENU_OPENED);

        private void OnFavoriteClicked(MinimapEvents.FavoriteClicked evt) =>
            analytics.Track(AnalyticsEvents.Minimap.FAVORITE_CLICKED, new JObject { { "is_favorite", evt.IsFavorite } });

        private void OnSkyboxWidgetToggled(MinimapEvents.SkyboxWidgetToggled evt) =>
            analytics.Track(AnalyticsEvents.Minimap.SKYBOX_WIDGET_TOGGLED, new JObject { { "is_open", evt.IsOpen } });

        private void OnSkyboxDayCycleToggled(MinimapEvents.SkyboxDayCycleToggled evt) =>
            analytics.Track(AnalyticsEvents.Minimap.SKYBOX_DAY_CYCLE_TOGGLED, new JObject { { "is_enabled", evt.IsEnabled } });

        private void OnSkyboxTimeChanged(MinimapEvents.SkyboxTimeChanged evt)
        {
            skyboxTimeCts = skyboxTimeCts.SafeRestart();
            TrackSkyboxTimeDebouncedAsync(evt.TimeOfDayNormalized, skyboxTimeCts.Token).Forget();
        }

        // The event arrives on every slider step, so only the value the user settles on is tracked.
        private async UniTaskVoid TrackSkyboxTimeDebouncedAsync(float timeOfDayNormalized, CancellationToken ct)
        {
            bool isCanceled = await UniTask.Delay(skyboxTimeDebounceMs, cancellationToken: ct).SuppressCancellationThrow();

            if (isCanceled) return;

            analytics.Track(AnalyticsEvents.Minimap.SKYBOX_TIME_CHANGED, new JObject { { "time_of_day", timeOfDayNormalized } });
        }
    }
}
