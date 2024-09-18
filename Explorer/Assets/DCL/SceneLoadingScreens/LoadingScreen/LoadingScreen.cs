using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility.Types;

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

        public async UniTask<Result> ShowWhileExecuteTaskAsync(
            Func<AsyncLoadProcessReport, UniTask> operation, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var timeout = LOADING_SCREEN_TIMEOUT_MINUTES;
            var loadReport = AsyncLoadProcessReport.Create();

            UniTask showLoadingScreenTask = mvcManager.ShowAsync(
                                                           SceneLoadingScreenController.IssueCommand(
                                                               new SceneLoadingScreenController.Params(loadReport, timeout)), ct)
                                                      .AttachExternalCancellation(ct);

            var result = Result.ErrorResult("Load Timeout!");

            async UniTask ExecuteOperationAsync()
            {
                try
                {
                    await operation(loadReport);
                    result = Result.SuccessResult();
                }
                catch (Exception e)
                {
                    loadReport.SetProgress(1f);
                    result = Result.ErrorResult(e.Message);
                }
            }

            await UniTask.WhenAny(showLoadingScreenTask, ExecuteOperationAsync());

            return result;
        }
    }
}
