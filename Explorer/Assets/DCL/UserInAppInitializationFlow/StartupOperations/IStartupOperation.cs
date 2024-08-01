using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public interface IStartupOperation
    {
        UniTask<StartupResult> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct);
    }

    public readonly struct StartupResult
    {
        public readonly bool Success;
        public readonly string? ErrorMessage;

        private StartupResult(bool success, string? errorMessage)
        {
            this.Success = success;
            this.ErrorMessage = errorMessage;
        }

        public static StartupResult SuccessResult() =>
            new (true, null);

        public static StartupResult ErrorResult(string errorMessage) =>
            new (false, errorMessage);
    }
}
