using System;
using DCL.Backpack.AvatarSection.Outfits.Events;
using DCL.PerformanceAndDiagnostics.Analytics;
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
            analytics.Track(AnalyticsEvents.Outfits.SAVE_OUTFIT);
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