using DCL.Backpack.Gifting.Events;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class GiftAnalytics
    {
        private readonly IAnalyticsController analytics;
        private readonly EventSubscriptionScope scope;

        public GiftAnalytics(IAnalyticsController analytics, IEventBus eventBus)
        {
            this.analytics = analytics;

            scope = new EventSubscriptionScope();
            scope.Add(eventBus.Subscribe<GiftingEvents.OnSuccessfullGift>(OnSuccessfullGift));
            scope.Add(eventBus.Subscribe<GiftingEvents.OnFailedGift>(OnFailedGift));
        }

        public void Dispose()
        {
            scope.Dispose();
        }

        private void OnSuccessfullGift(GiftingEvents.OnSuccessfullGift evt)
        {
            // // var wearablesArray = new JsonArray();
            // //
            // // foreach (string? urn in evt.WearablesUrns)
            // //     wearablesArray.Add(urn);
            //
            // var payload = new JsonObject
            // {
            //     {
            //         "wearables_urn", wearablesArray
            //     }
            // };

            analytics.Track(AnalyticsEvents.Gifts.SUCCESSFULL_GIFT);
        }

        private void OnFailedGift(GiftingEvents.OnFailedGift evt)
        {
            analytics.Track(AnalyticsEvents.Gifts.FAILED_GIFT);
        }
    }
}