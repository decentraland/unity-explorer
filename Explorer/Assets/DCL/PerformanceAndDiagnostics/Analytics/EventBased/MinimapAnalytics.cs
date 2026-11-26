using DCL.Minimap;
using Newtonsoft.Json.Linq;
using System;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    /// <summary>
    ///     Reports how the minimap is used. Realm and parcel are attached to every event by the common traits,
    ///     so only the action-specific values travel in the payload.
    /// </summary>
    public class MinimapAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly EventSubscriptionScope scope = new ();

        public MinimapAnalytics(IAnalyticsController analytics, IEventBus eventBus)
        {
            this.analytics = analytics;

            scope.Add(eventBus.Subscribe<MinimapEvents.Interaction>(OnInteraction));
        }

        public void Dispose() =>
            scope.Dispose();

        private void OnInteraction(MinimapEvents.Interaction interaction)
        {
            switch (interaction.Action)
            {
                case MinimapEvents.Action.Expanded:
                    analytics.Track(AnalyticsEvents.Minimap.EXPANDED);
                    break;
                case MinimapEvents.Action.Collapsed:
                    analytics.Track(AnalyticsEvents.Minimap.COLLAPSED);
                    break;
                case MinimapEvents.Action.MapClicked:
                    analytics.Track(AnalyticsEvents.Minimap.MAP_CLICKED);
                    break;
                case MinimapEvents.Action.FavoriteClicked:
                    analytics.Track(AnalyticsEvents.Minimap.FAVORITE_CLICKED, new JObject { { "is_favorite", interaction.State } });
                    break;
                case MinimapEvents.Action.ContextMenuOpened:
                    analytics.Track(AnalyticsEvents.Minimap.CONTEXT_MENU_OPENED);
                    break;
                case MinimapEvents.Action.SkyboxWidgetToggled:
                    analytics.Track(AnalyticsEvents.Minimap.SKYBOX_WIDGET_TOGGLED, new JObject { { "is_open", interaction.State } });
                    break;
                case MinimapEvents.Action.SkyboxTimeChanged:
                    analytics.Track(AnalyticsEvents.Minimap.SKYBOX_TIME_CHANGED, new JObject { { "time_of_day", interaction.TimeOfDay } });
                    break;
                case MinimapEvents.Action.SkyboxDayCycleToggled:
                    analytics.Track(AnalyticsEvents.Minimap.SKYBOX_DAY_CYCLE_TOGGLED, new JObject { { "is_enabled", interaction.State } });
                    break;
            }
        }
    }
}
