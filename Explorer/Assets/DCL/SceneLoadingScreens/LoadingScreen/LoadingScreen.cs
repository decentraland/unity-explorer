using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using MVC;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.SceneLoadingScreens.LoadingScreen
{
    public class LoadingScreen : ILoadingScreen
    {
        private readonly LoadingScreenTimeout loadingScreenTimeout;

        private readonly IMVCManager mvcManager;

        public LoadingScreen(IMVCManager mvcManager, LoadingScreenTimeout loadingScreenTimeout)
        {
            this.mvcManager = mvcManager;
            this.loadingScreenTimeout = loadingScreenTimeout;
        }

        /// <summary>
        ///     Binds the internal operation, asyncLoadProcessReport, and the loading Screen together so they can't finish at different time
        /// </summary>
        public async UniTask<Result> ShowWhileExecuteTaskAsync(
            Func<AsyncLoadProcessReport, CancellationToken, UniTask<Result>> operation, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return Result.CancelledResult();

            // Loading report will be the only source of truth for results and cancellation
            var timeOut = new CancellationTokenSource();
            var operationFinished = new CancellationTokenSource();

            var loadReport = AsyncLoadProcessReport.Create(timeOut.Token);

            // Timeout will fire if the parent cancellation is fired or the operation takes too long
            async UniTask<Result> ExecuteTimeOutOrCancelled()
            {
                bool isCancelled = await UniTask.Delay(loadingScreenTimeout.Value, cancellationToken: ct).SuppressCancellationThrow();

                return isCancelled ? Result.CancelledResult() : Result.ErrorResult("Load Timeout!");
            }

            async UniTask<Result> ExecuteOperationAsync()
            {
                Result result = await operation(loadReport, timeOut.Token);

                // if the operation has fully succeeded:
                // 1. Set the progress to 1.0f
                // 2. Cancel the loading screen

                // if the internal operation didn't modify the loading report on its own, finalize it
                if (result.Success)
                    loadReport.SetProgress(1.0f);
                else
                    loadReport.SetException(new Exception(result.ErrorMessage));

                return result;
            }

            Result? finalResult = null;

            async UniTask WaitForOperationResultOrTimeout()
            {
                // one or another
                (int winArgumentIndex, Result opResult, Result timeoutResult) = await UniTask.WhenAny(ExecuteOperationAsync(), ExecuteTimeOutOrCancelled());

                switch (winArgumentIndex)
                {
                    case 0:
                        finalResult = opResult;
                        operationFinished.Cancel();
                        operationFinished.Dispose();
                        return;
                    case 1:
                        finalResult = timeoutResult;
                        timeOut.Cancel();
                        timeOut.Dispose();
                        return;
                    default:
                        finalResult = Result.ErrorResult("Unexpected winArgumentIndex: " + winArgumentIndex);
                        return;
                }
            }

            async UniTask<Result> ExecuteLoadingScreen()
            {
                // The loading screen will be bound via load report 1-to-1 as ExecuteOperationAsync ensures the state of LoadReport:
                // 1. if the operation has finished -> cancel the loading screen with Fade
                // 2. if timeout has fired -> cancel the loading screen with Fade
                // 3. if the outer cancellation token has fired -> cancel the loading screen immediately

                Result result = await mvcManager.ShowAsync(
                                                     SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport)), ct)
                                                .SuppressToResultAsync(ReportCategory.SCENE_LOADING);

                if (loadReport.GetStatus().TaskStatus == UniTaskStatus.Pending)
                    ReportHub.LogError(ReportCategory.SCENE_LOADING, "Loading screen finished unexpectedly, but the loading process continues");

                return result;
            }

            await UniTask.WhenAll(WaitForOperationResultOrTimeout(), ExecuteLoadingScreen());
            return finalResult!.Value;
        }
    }
}
