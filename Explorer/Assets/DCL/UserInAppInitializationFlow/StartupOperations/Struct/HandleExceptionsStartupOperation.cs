using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.StartupOperations.Struct
{
    public class HandleExceptionsStartupOperation : IStartupOperation
    {
        private readonly IStartupOperation origin;

        public HandleExceptionsStartupOperation(IStartupOperation origin)
        {
            this.origin = origin;
        }

        public async UniTask<StartupResult> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            try { return await origin.ExecuteAsync(report, ct); }
            catch (Exception e) { return StartupResult.ErrorResult(e.Message ?? $"Unknown error during the starting process: {e.GetType()!.Name}"); }
        }
    }
}
