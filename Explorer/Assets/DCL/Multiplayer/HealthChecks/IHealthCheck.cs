using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Multiplayer.HealthChecks
{
    public interface IHealthCheck
    {
        UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync(CancellationToken ct);
    }
}
