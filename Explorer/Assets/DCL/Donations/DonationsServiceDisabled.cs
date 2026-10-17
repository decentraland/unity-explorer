using Cysharp.Threading.Tasks;
using DCL.Utilities;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Donations
{
    public class DonationsServiceDisabled : IDonationsService
    {
        public IReadonlyReactiveProperty<(bool enabled, string? creatorAddress, Vector2Int? baseParcel)> DonationsEnabledCurrentScene => donationsEnabledCurrentScene;
        private readonly ReactiveProperty<(bool enabled, string? creatorAddress, Vector2Int? baseParcel)> donationsEnabledCurrentScene = new ((false, null, null));
        public bool DonationFeatureEnabled => false;

        public void Dispose() {}

        public UniTask<string> GetSceneNameAsync(Vector2Int parcelPosition, CancellationToken ct) =>
            throw new NotImplementedException();

        public UniTask<decimal> GetCurrentBalanceAsync(CancellationToken ct) =>
            throw new NotImplementedException();

        public UniTask<bool> SendDonationAsync(string toAddress, decimal amountInMana, CancellationToken ct) =>
            throw new NotImplementedException();

        public UniTask<decimal> GetCurrentManaConversionAsync(CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
