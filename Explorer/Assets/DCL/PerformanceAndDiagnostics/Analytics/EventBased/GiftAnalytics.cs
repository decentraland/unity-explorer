using DCL.Backpack.Gifting.Events;
using Segment.Serialization;
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
            scope.Add(eventBus.Subscribe<GiftingEvents.OnSuccessfulGift>(OnSuccessfulGift));
            scope.Add(eventBus.Subscribe<GiftingEvents.OnFailedGift>(OnFailedGift));
        }

        public void Dispose()
        {
            scope.Dispose();
        }

        private void OnSuccessfulGift(GiftingEvents.OnSuccessfulGift evt)
        {
            var payload = new JsonObject
            {
                {
                    "item", evt.ItemUrn
                },
                {
                    "sender", evt.SenderAddress
                },
                {
                    "receiver", evt.ReceiverAddress
                },
                {
                    "item_type", evt.ItemType
                }
            };

            analytics.Track(AnalyticsEvents.Gifts.SUCCESSFULL_GIFT, payload);
        }

        private void OnFailedGift(GiftingEvents.OnFailedGift evt)
        {
            var payload = new JsonObject
            {
                {
                    "item", evt.ItemUrn
                },
                {
                    "sender", evt.SenderAddress
                },
                {
                    "receiver", evt.ReceiverAddress
                },
                {
                    "item_type", evt.ItemType
                }
            };

            analytics.Track(AnalyticsEvents.Gifts.FAILED_GIFT, payload);
        }
    }
}