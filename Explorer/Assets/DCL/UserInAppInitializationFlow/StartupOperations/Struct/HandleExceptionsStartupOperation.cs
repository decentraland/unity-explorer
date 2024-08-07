using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations.Struct
{
    public class HandleExceptionsStartupOperation : IStartupOperation
    {
        private readonly IStartupOperation origin;

        public HandleExceptionsStartupOperation(IStartupOperation origin)
        {
            this.origin = origin;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            try { return await origin.ExecuteAsync(report, ct); }
            catch (Exception e) { return Result.ErrorResult(e.Message ?? $"Unknown error during the starting process: {e.GetType()!.Name}"); }
        }
    }
}
