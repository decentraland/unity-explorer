using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.SceneLoadingScreens.LoadingScreen
{
    public interface ILoadingScreen
    {
        UniTask<Result> ShowWhileExecuteTaskAsync(Func<IAsyncLoadProcessReport, UniTask<Result>> operation,
            CancellationToken ct);

        class EmptyLoadingScreen : ILoadingScreen
        {
            public async UniTask<Result> ShowWhileExecuteTaskAsync(
                Func<IAsyncLoadProcessReport, UniTask<Result>> operation,
                CancellationToken ct)
            {
                var loadReport = AsyncLoadProcessReport.Create();
                try
                {
                    await operation(loadReport);
                    return Result.SuccessResult();
                }
                catch (Exception e)
                {
                    loadReport.SetProgress(1f, $"Error: {e.Message}");
                    return Result.ErrorResult(e.Message ?? "Unknown error");
                }
            }
        }
    }
}
