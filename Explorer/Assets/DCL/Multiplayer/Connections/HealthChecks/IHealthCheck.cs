using Cysharp.Threading.Tasks;

namespace DCL.Multiplayer.Connections.HealthChecks
{
    public interface IHealthCheck
    {
        UniTask<bool> IsRemoteAvailableAsync();
    }
}
