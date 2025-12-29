using System.Threading;
using Cysharp.Threading.Tasks;

namespace DCL.Backpack.Gifting.Services.SnapshotEquipped
{
    public interface IAvatarEquippedStatusProvider
    {
        UniTask InitializeAsync(CancellationToken ct);
        bool IsEquipped(string urn);
        void LogEquippedStatus();
    }
}