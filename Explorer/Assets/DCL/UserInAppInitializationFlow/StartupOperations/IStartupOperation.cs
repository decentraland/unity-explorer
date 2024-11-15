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
}
