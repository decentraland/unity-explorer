using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using MVC;
using System;
using System.Threading;

namespace DCL.SceneLoadingScreens.LoadingScreen
{
    public class LoadingScreen : ILoadingScreen
    {
        public static readonly TimeSpan LOADING_SCREEN_TIMEOUT_MINUTES = TimeSpan.FromMinutes(2);
        private readonly IMVCManager mvcManager;

        public LoadingScreen(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public async UniTask ShowWhileExecuteTaskAsync(Func<AsyncLoadProcessReport, UniTask> operation, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var timeout = LOADING_SCREEN_TIMEOUT_MINUTES;
            var loadReport = AsyncLoadProcessReport.Create();
            UniTask showLoadingScreenTask = mvcManager.ShowAsync(
                                                           SceneLoadingScreenController.IssueCommand(
                                                               new SceneLoadingScreenController.Params(loadReport, timeout)), ct)
                                                      .AttachExternalCancellation(ct);

            UniTask operationTask = operation(loadReport);

            await UniTask.WhenAll(showLoadingScreenTask, operationTask);
        }
    }
}
