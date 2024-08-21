using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System;
using System.Threading;

namespace DCL.SceneLoadingScreens.LoadingScreen
{
    public interface ILoadingScreen
    {
        enum ShowResult
        {
            Success,
            Timeout,
        }

        UniTask<ShowResult> ShowWhileExecuteTaskAsync(Func<AsyncLoadProcessReport, UniTask> operation, CancellationToken ct);

        class EmptyLoadingScreen : ILoadingScreen
        {
            public async UniTask<ShowResult> ShowWhileExecuteTaskAsync(Func<AsyncLoadProcessReport, UniTask> operation, CancellationToken ct)
            {
                await operation(AsyncLoadProcessReport.Create());
                return ShowResult.Success;
            }
        }
    }
}
