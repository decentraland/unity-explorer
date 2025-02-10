using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities;
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
        public async UniTask<EnumResult<TaskError>> ShowWhileExecuteTaskAsync(
            Func<AsyncLoadProcessReport, CancellationToken, UniTask<EnumResult<TaskError>>> operation, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);

            // Loading report will be the only source of truth for results and cancellation
            var timeOut = new CancellationTokenSource();
            var operationFinished = new CancellationTokenSource();

            var loadReport = AsyncLoadProcessReport.Create(timeOut.Token);

            // Timeout will fire if the parent cancellation is fired or the operation takes too long
            async UniTask<TaskError> ExecuteTimeOutOrCancelledAsync()
            {
                bool isCancelled = await UniTask.Delay(loadingScreenTimeout.Value, cancellationToken: ct).SuppressCancellationThrow();
                return isCancelled ? TaskError.Cancelled : TaskError.Timeout;
            }

            async UniTask<EnumResult<TaskError>> ExecuteOperationAsync()
            {
                EnumResult<TaskError> result = await operation(loadReport, timeOut.Token);
                loadReport.SetResult(result.AsResult());
                return result;
            }

            EnumResult<TaskError>? finalResult = null;

            async UniTask WaitForOperationResultOrTimeoutAsync()
            {
                // one or another
                (int winArgumentIndex, EnumResult<TaskError> opResult, TaskError timeoutResult) = await UniTask.WhenAny(ExecuteOperationAsync(), ExecuteTimeOutOrCancelledAsync());

                switch (winArgumentIndex)
                {
                    case 0:
                        finalResult = opResult;
                        operationFinished.Cancel();
                        operationFinished.Dispose();
                        return;
                    case 1:
                        finalResult = EnumResult<TaskError>.ErrorResult(timeoutResult);
                        timeOut.Cancel();
                        timeOut.Dispose();
                        return;
                    default:
                        finalResult = EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "Unexpected winArgumentIndex: " + winArgumentIndex);
                        return;
                }
            }

            async UniTask<EnumResult<TaskError>> ExecuteLoadingScreenAsync()
            {
                // The loading screen will be bound via load report 1-to-1 as ExecuteOperationAsync ensures the state of LoadReport:
                // 1. if the operation has finished -> cancel the loading screen with Fade
                // 2. if timeout has fired -> cancel the loading screen with Fade
                // 3. if the outer cancellation token has fired -> cancel the loading screen immediately

                var result = await mvcManager.ShowAsync(
                                                     SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport)), ct)
                                                .SuppressToResultAsync(ReportCategory.SCENE_LOADING);

                if (loadReport.GetStatus().TaskStatus == UniTaskStatus.Pending)
                    ReportHub.LogError(ReportCategory.SCENE_LOADING, "Loading screen finished unexpectedly, but the loading process continues");

                if (finalResult.HasValue && !result.Success)
                    ReportHub.LogError(ReportCategory.SCENE_LOADING, $"Loading screen finished with an error after the flow has finished: {result.Error.AsMessage()}");

                return result;
            }

            await UniTask.WhenAll(WaitForOperationResultOrTimeoutAsync(), ExecuteLoadingScreenAsync());
            return finalResult!.Value;
        }
    }
}
