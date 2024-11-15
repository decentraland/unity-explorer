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
        private readonly TimeSpan loadingScreenTimeout;

        private readonly IMVCManager mvcManager;

        public LoadingScreen(IMVCManager mvcManager) : this(mvcManager, TimeSpan.FromMinutes(2))
        {
            this.mvcManager = mvcManager;
        }

        public LoadingScreen(IMVCManager mvcManager, TimeSpan loadingScreenTimeout)
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

            var loadReport = AsyncLoadProcessReport.Create(timeOut.Token);

            // Bind loading screen with load report 1-to-1 via a cancellation token

            UniTask<Result> showLoadingScreenTask = mvcManager.ShowAsync(
                                                                   SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport)), timeOut.Token)
                                                              .SuppressToResultAsync(ReportCategory.SCENE_LOADING);

            // Preserve timeout result, otherwise it's super-seeded by ExecuteOperationAsync, and
            // there will be no difference between Cancelled and Timeout
            Result? firedTimeout = null;

            // Timeout will fire if the parent cancellation is fired or the operation takes too long
            async UniTask<Result> ExecuteTimeOutOrCancelled()
            {
                bool isCancelled = await UniTask.Delay(loadingScreenTimeout, cancellationToken: ct).SuppressCancellationThrow();

                if (isCancelled) return Result.CancelledResult();

                firedTimeout = Result.ErrorResult("Load Timeout!");
                timeOut.Cancel();
                timeOut.Dispose();
                return firedTimeout!.Value;
            }

            async UniTask<Result> ExecuteOperationAsync()
            {
                Result result = await operation(loadReport, timeOut.Token);

                if (firedTimeout.HasValue)
                    result = firedTimeout.Value;

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

            (int winArgumentIndex, Result opResult, Result timeoutResult, Result loadingScreenResult)
                = await UniTask.WhenAny(ExecuteOperationAsync(), ExecuteTimeOutOrCancelled(), showLoadingScreenTask);

            switch (winArgumentIndex)
            {
                case 0: return opResult;
                case 1: return timeoutResult;
                default:
                    ReportHub.LogError(ReportCategory.SCENE_LOADING, "Loading screen finished unexpectedly, but the loading process continues");
                    return loadingScreenResult;
            }
        }
    }
}
