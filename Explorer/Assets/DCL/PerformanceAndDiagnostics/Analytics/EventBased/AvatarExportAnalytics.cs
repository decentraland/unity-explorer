using System;
using DCL.AvatarRendering.Export;
using Segment.Serialization;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
	public class AvatarExportAnalytics : IDisposable
	{
		private readonly IAnalyticsController analytics;
		private readonly EventSubscriptionScope scope;
		
		public AvatarExportAnalytics(IAnalyticsController analytics, IEventBus eventBus)
		{
			this.analytics = analytics;
			scope = new EventSubscriptionScope();

			scope.Add(eventBus.Subscribe<AvatarExportEvents>(OnHomeChanged));
		}

		public void Dispose() => scope.Dispose();

		private void OnHomeChanged(AvatarExportEvents avatarExportEvents)
		{
			analytics.Track(AnalyticsEvents.Wearables.AVATAR_EXPORTED_TO_VRM, new JsonObject
			{
				{ "succeed", avatarExportEvents.Succeeded },
			});
		}
	}
}

