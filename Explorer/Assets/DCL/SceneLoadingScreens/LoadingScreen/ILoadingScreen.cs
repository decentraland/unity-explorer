using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.SceneLoadingScreens.LoadingScreen
{
    public interface ILoadingScreen
    {
        UniTask<Result> ShowWhileExecuteTaskAsync(Func<AsyncLoadProcessReport, CancellationToken, UniTask<Result>> operation,
            CancellationToken ct);

        class EmptyLoadingScreen : ILoadingScreen
        {
            public async UniTask<Result> ShowWhileExecuteTaskAsync(
                Func<AsyncLoadProcessReport, CancellationToken, UniTask<Result>> operation,
                CancellationToken ct)
            {
                var loadReport = AsyncLoadProcessReport.Create(ct);
                try
                {
                    await operation(loadReport, ct);
                    return Result.SuccessResult();
                }
                catch (Exception e)
                {
                    loadReport.SetProgress(1f);
                    return Result.ErrorResult(e.Message);
                }
            }
        }
    }
}
