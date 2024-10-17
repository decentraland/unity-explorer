using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using MVC;
using System;
using System.Threading;
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
            Func<IAsyncLoadProcessReport, UniTask<Result>> operation,
            CancellationToken ct
        )
        {
            if (Result.ErrorResultIfCancelled(ct, out var result))
                return result;

            IAsyncLoadProcessReport loadReport = AsyncLoadProcessReport.Create();

            result = Result.ErrorResult("Unknown");

            async UniTask ExecuteScreenAsync()
            {
                await mvcManager.ShowAsync(SceneLoadingScreenController.IssueCommand(NewParams(loadReport)), ct)
                                .AttachExternalCancellation(ct);

                result = Result.ErrorResult("Load Timeout! Executing on an operation - {}");
            }

            async UniTask ExecuteOperationAsync()
            {
                result = await operation(loadReport);
            }

            await UniTask.WhenAny(ExecuteScreenAsync(), ExecuteOperationAsync());

            return result;
        }

        private static SceneLoadingScreenController.Params NewParams(IAsyncLoadProcessReport loadReport) =>
            new (
                loadReport,
                LOADING_SCREEN_TIMEOUT_MINUTES
            );
    }
}
