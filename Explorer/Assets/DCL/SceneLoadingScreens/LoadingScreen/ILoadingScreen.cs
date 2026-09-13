using Cysharp.Threading.Tasks;
using DCL.Utilities;
using DCL.Utility.Types;
using System;
using System.Threading;

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
                EnumResult<TaskError> result;

                try
                {
                    result = await operation(loadReport, ct);
                }
                catch (OperationCanceledException)
                {
                    result = EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);
                }
                catch (Exception e)
                {
                    result = EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message, e);
                }

                if (ct.IsCancellationRequested || result.Error is { State: TaskError.Cancelled } or { Exception: OperationCanceledException })
                {
                    result = EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);
                    loadReport.SetCancelled();
                }
                else
                    loadReport.SetResult(result.AsResult());

                await loadReport.WaitUntilFinishedAsync();
                return result;
            }
        }
    }
}
