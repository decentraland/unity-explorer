using System;
using DCL.Backpack.AvatarSection.Outfits.Events;
using DCL.PerformanceAndDiagnostics.Analytics;
using Newtonsoft.Json.Linq;
using Utility;

namespace DCL.Backpack.AvatarSection.Outfits.Analytics
{
    public class OutfitsAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly EventSubscriptionScope scope;

        public OutfitsAnalytics(IAnalyticsController analytics, IEventBus eventBus)
        {
            this.analytics = analytics;

            scope = new EventSubscriptionScope();
            scope.Add(eventBus.Subscribe<OutfitsEvents.SaveOutfitEvent>(OnSaveOutfit));
            scope.Add(eventBus.Subscribe<OutfitsEvents.EquipOutfitEvent>(OnEquipOutfit));
            scope.Add(eventBus.Subscribe<OutfitsEvents.ClaimExtraOutfitsEvent>(OnClaimExtraOutfits));
        }

        public void Dispose()
        {
            scope.Dispose();
        }

        private void OnSaveOutfit(OutfitsEvents.SaveOutfitEvent evt)
        {
            var wearablesArray = new JArray();

            foreach (string? urn in evt.WearablesUrns)
                wearablesArray.Add(urn);

            var payload = new JObject
            {
                {
                    "wearables_urn", wearablesArray
                }
            };

            analytics.Track(AnalyticsEvents.Outfits.SAVE_OUTFIT, payload);
        }

        private void OnEquipOutfit(OutfitsEvents.EquipOutfitEvent evt)
        {
            analytics.Track(AnalyticsEvents.Outfits.EQUIP_OUTFIT);
        }

        private void OnClaimExtraOutfits(OutfitsEvents.ClaimExtraOutfitsEvent evt)
        {
            analytics.Track(AnalyticsEvents.Outfits.OUTFIT_CLICK_NAME);
        }
    }
}
