using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.UserInAppInitializationFlow.StartupOperations.Struct;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public interface IStartupOperation
    {
        UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct);
    }

    public static class StartupOperationExtensions
    {
        public static IStartupOperation WithHandleExceptions(this IStartupOperation origin) =>
            new HandleExceptionsStartupOperation(origin);
    }
}
