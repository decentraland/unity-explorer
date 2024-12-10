using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Utility.Types;

namespace DCL.Multiplayer.HealthChecks.Struct
{
    public class SequentialHealthCheck : IHealthCheck
    {
        private readonly IReadOnlyList<IHealthCheck> list;

        public SequentialHealthCheck(params IHealthCheck[] list)
        {
            this.list = list;
        }

        public async UniTask<Result> IsRemoteAvailableAsync(CancellationToken ct)
        {
            foreach (IHealthCheck healthCheck in list)
            {
                var result = await healthCheck.IsRemoteAvailableAsync(ct);
                if (result.Success == false) return result;
            }

            return Result.SuccessResult();
        }
    }
}
