using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.SceneLoadingScreens.LoadingScreen
{
    public interface ILoadingScreen
    {
        UniTask<EnumResult<TaskError>> ShowWhileExecuteTaskAsync(
            Func<AsyncLoadProcessReport, CancellationToken, UniTask<EnumResult<TaskError>>> operation,
            CancellationToken ct
        );

        class EmptyLoadingScreen : ILoadingScreen
        {
            public async UniTask<EnumResult<TaskError>> ShowWhileExecuteTaskAsync(
                Func<AsyncLoadProcessReport, CancellationToken, UniTask<EnumResult<TaskError>>> operation,
                CancellationToken ct)
            {
                var loadReport = AsyncLoadProcessReport.Create(ct);

                try
                {
                    await operation(loadReport, ct);
                    return EnumResult<TaskError>.SuccessResult();
                }
                catch (Exception e)
                {
                    loadReport.SetProgress(1f);
                    return EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message);
                }
            }
        }
    }
}
