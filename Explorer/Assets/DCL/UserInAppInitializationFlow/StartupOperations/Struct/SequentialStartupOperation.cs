using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System.Collections.Generic;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations.Struct
{
    public class SequentialStartupOperation : IStartupOperation
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly IReadOnlyList<IStartupOperation> operations;

        public SequentialStartupOperation(ILoadingStatus loadingStatus, params IStartupOperation[] operations)
        {
            this.loadingStatus = loadingStatus;
            this.operations = operations;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            foreach (IStartupOperation startupOperation in operations)
            {
                var result = await startupOperation.ExecuteAsync(report, ct);

                if (result.Success == false)
                {
                    report.SetProgress(loadingStatus.SetCompletedStage(LoadingStatus.Stage.Completed));
                    report.SetProgress(1);
                    return result;
                }
            }

            return Result.SuccessResult();
        }
    }
}
