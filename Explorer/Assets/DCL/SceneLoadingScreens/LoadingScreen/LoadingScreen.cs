using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using MVC;
using System;
using System.Threading;

namespace DCL.SceneLoadingScreens.LoadingScreen
{
    public class LoadingScreen : ILoadingScreen
    {
        private readonly IMVCManager mvcManager;

        public LoadingScreen(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public async UniTask ShowWhileExecuteTaskAsync(Func<AsyncLoadProcessReport, UniTask> operation, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var timeout = TimeSpan.FromSeconds(30);
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
