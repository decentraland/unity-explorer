using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.SceneLoadingScreens.LoadingScreen
{
    public class LoadingScreen : ILoadingScreen
    {
        private static readonly TimeSpan LOADING_SCREEN_TIMEOUT_MINUTES = TimeSpan.FromMinutes(2);
        private readonly IMVCManager mvcManager;

        public LoadingScreen(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public async UniTask<ILoadingScreen.LoadResult> ShowWhileExecuteTaskAsync(
            Func<AsyncLoadProcessReport, UniTask> operation, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var timeout = LOADING_SCREEN_TIMEOUT_MINUTES;
            var loadReport = AsyncLoadProcessReport.Create();

            UniTask showLoadingScreenTask = mvcManager.ShowAsync(
                                                           SceneLoadingScreenController.IssueCommand(
                                                               new SceneLoadingScreenController.Params(loadReport, timeout)), ct)
                                                      .AttachExternalCancellation(ct);

            var result = ILoadingScreen.LoadResult.TimeoutResult;

            async UniTask ExecuteOperationAsync()
            {
                try
                {
                    await operation(loadReport);
                    result = ILoadingScreen.LoadResult.SuccessResult;
                }
                catch (Exception e)
                {
                    result = ILoadingScreen.LoadResult.ExceptionResult(e.Message);
                }
            }

            await UniTask.WhenAny(showLoadingScreenTask, ExecuteOperationAsync());

            return result;
        }
    }
}
