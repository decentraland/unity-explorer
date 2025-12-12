using Cysharp.Threading.Tasks;
using DCL.Donations;
using DCL.Utilities;
using MVC;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics.DecoratorBased
{
    public class DonationsServiceAnalyticsDecorator : IDonationsService
    {
        private readonly IDonationsService core;
        private readonly IAnalyticsController analytics;

        public IReadonlyReactiveProperty<(bool enabled, string? creatorAddress, Vector2Int? baseParcel)> DonationsEnabledCurrentScene => core.DonationsEnabledCurrentScene;
        public bool DonationFeatureEnabled => core.DonationFeatureEnabled;

        public DonationsServiceAnalyticsDecorator(IDonationsService core, IAnalyticsController analytics)
        {
            this.core = core;
            this.analytics = analytics;
        }

        public void Dispose() => core.Dispose();

        public async UniTask<string> GetSceneNameAsync(Vector2Int parcelPosition, CancellationToken ct) =>
            await core.GetSceneNameAsync(parcelPosition, ct);

        public async UniTask<decimal> GetCurrentBalanceAsync(CancellationToken ct) =>
            await core.GetCurrentBalanceAsync(ct);

        public async UniTask<bool> SendDonationAsync(string toAddress, decimal amountInMana, CancellationToken ct)
        {
            var from = ViewDependencies.CurrentIdentity?.Address.ToString();

            JObject json = new JObject
            {
                ["from"] = from,
                ["to"] = toAddress,
                ["mana"] = amountInMana,
                ["usd"] = await GetCurrentManaConversionAsync(ct) * amountInMana
            };

            try
            {
                analytics.Track(AnalyticsEvents.Donations.DONATION_STARTED, json);

                bool result = await core.SendDonationAsync(toAddress, amountInMana, ct);

                json["success"] = result;
                analytics.Track(AnalyticsEvents.Donations.DONATION_ENDED, json);

                return result;
            }
            catch (Exception)
            {
                json["success"] = false;
                analytics.Track(AnalyticsEvents.Donations.DONATION_ENDED, json);

                throw;
            }
        }

        public async UniTask<decimal> GetCurrentManaConversionAsync(CancellationToken ct) =>
            await core.GetCurrentManaConversionAsync(ct);
    }
}
