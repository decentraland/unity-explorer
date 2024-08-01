using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System.Collections.Generic;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.StartupOperations.Struct
{
    public class SeveralStartupOperation : IStartupOperation
    {
        private readonly IReadOnlyList<IStartupOperation> operations;

        public SeveralStartupOperation(params IStartupOperation[] operations)
        {
            this.operations = operations;
        }

        public async UniTask<StartupResult> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            foreach (IStartupOperation startupOperation in operations)
            {
                var result = await startupOperation.ExecuteAsync(report, ct);

                if (result.Success == false)
                {
                    report.SetProgress(1);
                    return result;
                }
            }

            return StartupResult.SuccessResult();
        }
    }
}
