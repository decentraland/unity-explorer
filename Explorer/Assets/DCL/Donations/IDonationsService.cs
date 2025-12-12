using Cysharp.Threading.Tasks;
using DCL.Utilities;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Donations
{
    public interface IDonationsService : IDisposable
    {
        public IReadonlyReactiveProperty<(bool enabled, string? creatorAddress, Vector2Int? baseParcel)> DonationsEnabledCurrentScene { get; }
        public bool DonationFeatureEnabled { get; }

        public UniTask<string> GetSceneNameAsync(Vector2Int parcelPosition, CancellationToken ct);
        public UniTask<decimal> GetCurrentBalanceAsync(CancellationToken ct);
        public UniTask<bool> SendDonationAsync(string toAddress, decimal amountInMana, CancellationToken ct);
        public UniTask<decimal> GetCurrentManaConversionAsync(CancellationToken ct);
    }
}
