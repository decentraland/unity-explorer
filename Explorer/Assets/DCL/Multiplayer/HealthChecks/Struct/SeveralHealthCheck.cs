using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.HealthChecks.Struct
{
    public class SeveralHealthCheck : IHealthCheck
    {
        private readonly IReadOnlyList<IHealthCheck> list;

        public SeveralHealthCheck(params IHealthCheck[] list)
        {
            this.list = list;
        }

        public async UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync(CancellationToken ct)
        {
            foreach (IHealthCheck healthCheck in list)
            {
                var result = await healthCheck.IsRemoteAvailableAsync(ct);
                if (result.success == false) return result;
            }

            return (true, null);
        }
    }
}
