using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class EnsureLivekitConnectionStartupOperation : IStartupOperation
    {
        public async UniTask<StartupResult> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            return StartupResult.ErrorResult("Something wrong");
        }
    }
}
