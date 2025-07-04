using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Utility.Types;

namespace DCL.Multiplayer.HealthChecks.Struct
{
    public class ParallelHealthCheck : IHealthCheck
    {
        private readonly IReadOnlyList<IHealthCheck> list;
        private readonly List<UniTask<Result>> temp = new ();

        public ParallelHealthCheck(params IHealthCheck[] list)
        {
            this.list = list;
        }

        public async UniTask<Result> IsRemoteAvailableAsync(CancellationToken ct)
        {
            temp.Clear();

            foreach (IHealthCheck healthCheck in list)
                temp.Add(healthCheck.IsRemoteAvailableAsync(ct));

            Stopwatch stopwatch = Stopwatch.StartNew();
            UnityEngine.Debug.Log("JUANI HEALTH CHECK START");
            var result = await UniTask.WhenAll(temp);
            stopwatch.Stop();
            UnityEngine.Debug.Log($"JUANI HEALTH CHECK END {stopwatch.ElapsedMilliseconds}");


            foreach (var r in result)
                if (r.Success == false)
                    return r;

            return Result.SuccessResult();
        }
    }
}
