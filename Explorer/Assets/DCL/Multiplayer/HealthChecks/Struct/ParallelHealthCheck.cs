using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.HealthChecks.Struct
{
    public class ParallelHealthCheck : IHealthCheck
    {
        private readonly IReadOnlyList<IHealthCheck> list;
        private readonly List<UniTask<(bool success, string? errorMessage)>> temp = new ();

        public ParallelHealthCheck(params IHealthCheck[] list)
        {
            this.list = list;
        }

        public async UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync(CancellationToken ct)
        {
            temp.Clear();

            foreach (IHealthCheck healthCheck in list)
                temp.Add(healthCheck.IsRemoteAvailableAsync(ct));

            var result = await UniTask.WhenAll(temp);

            foreach (var r in result)
                if (r.success == false)
                    return r;

            return (true, null);
        }
    }
}
