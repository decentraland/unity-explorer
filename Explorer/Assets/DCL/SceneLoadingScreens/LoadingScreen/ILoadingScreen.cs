using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System;
using System.Threading;
using UnityEngine.Serialization;

namespace DCL.SceneLoadingScreens.LoadingScreen
{
    public interface ILoadingScreen
    {
        struct LoadResult
        {
            public string ErrorMessage;
            public bool Success;

            public static LoadResult SuccessResult => new() { Success = true };
            public static LoadResult TimeoutResult => ExceptionResult("Load timeout!");

            public static LoadResult ExceptionResult(string errorMessage)
            {
                return new LoadResult() { Success = false, ErrorMessage = errorMessage };
            }
        }

        UniTask<LoadResult> ShowWhileExecuteTaskAsync(Func<AsyncLoadProcessReport, UniTask> operation,
            CancellationToken ct);

        class EmptyLoadingScreen : ILoadingScreen
        {
            public async UniTask<LoadResult> ShowWhileExecuteTaskAsync(Func<AsyncLoadProcessReport, UniTask> operation,
                CancellationToken ct)
            {
                await operation(AsyncLoadProcessReport.Create());
                return new LoadResult { Success = true };
            }
        }
    }
}
