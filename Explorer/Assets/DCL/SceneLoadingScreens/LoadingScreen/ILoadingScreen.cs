using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System;
using System.Threading;
using UnityEngine.Serialization;
using Utility.Types;

namespace DCL.SceneLoadingScreens.LoadingScreen
{
    public interface ILoadingScreen
    {
        UniTask<Result> ShowWhileExecuteTaskAsync(Func<AsyncLoadProcessReport, UniTask> operation,
            CancellationToken ct);

        class EmptyLoadingScreen : ILoadingScreen
        {
            public async UniTask<Result> ShowWhileExecuteTaskAsync(Func<AsyncLoadProcessReport, UniTask> operation,
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
                    loadReport.SetProgress(1f);
                    return Result.ErrorResult(e.Message);
                }
            }
        }
    }
}
