using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace DCL.Multiplayer.HealthChecks
{
    public class SeveralHealthCheck : IHealthCheck
    {
        private readonly IReadOnlyList<IHealthCheck> list;

        public SeveralHealthCheck(params IHealthCheck[] list)
        {
            this.list = list;
        }

        public async UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync()
        {
            foreach (IHealthCheck healthCheck in list)
            {
                var result = await healthCheck.IsRemoteAvailableAsync();
                if (result.success == false) return result;
            }

            return (true, null);
        }
    }
}
