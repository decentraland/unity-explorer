using System;
using DCL.MapRenderer.MapLayers.HomeMarker;
using Segment.Serialization;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
	public class HomeMarkerAnalytics : IDisposable
	{
		private readonly IAnalyticsController analytics;
		private readonly EventSubscriptionScope scope;

		public HomeMarkerAnalytics(IAnalyticsController analytics, IEventBus eventBus)
		{
			this.analytics = analytics;
			scope = new EventSubscriptionScope();
			
			scope.Add(eventBus.Subscribe<HomeMarkerEvents.MessageHomePositionChanged>(OnHomeChanged));
		}

		public void Dispose()
		{
			scope.Dispose();
		}

		private void OnHomeChanged(HomeMarkerEvents.MessageHomePositionChanged evt)
		{
			var properties = new JsonObject
			{
				{ "is_home_set", evt.IsHomeSet },
				{ "coordinates", evt.Coordinates.ToString() }
			};
			
			analytics.Track(AnalyticsEvents.UI.HOME_POSITION_SET, properties);
		}

		
	}
}