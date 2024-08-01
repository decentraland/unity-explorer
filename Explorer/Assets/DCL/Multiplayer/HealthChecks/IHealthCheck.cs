using Cysharp.Threading.Tasks;

namespace DCL.Multiplayer.HealthChecks
{
    public interface IHealthCheck
    {
        UniTask<(bool success, string errorMessage)> IsRemoteAvailableAsync();
    }
}
