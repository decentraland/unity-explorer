using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System;
using System.Threading;

namespace DCL.SceneLoadingScreens.LoadingScreen
{
    public interface ILoadingScreen
    {
        UniTask ShowWhileExecuteTaskAsync(Func<AsyncLoadProcessReport, UniTask> operation, CancellationToken ct);
    }
}
