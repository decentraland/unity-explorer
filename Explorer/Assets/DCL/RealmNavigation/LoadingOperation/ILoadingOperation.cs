using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System.Threading;
using Utility.Types;

namespace DCL.RealmNavigation.LoadingOperation
{
    public interface ILoadingOperation<in TParams> where TParams: ILoadingOperationParams
    {
        UniTask<EnumResult<TaskError>> ExecuteAsync(TParams args, CancellationToken ct);
    }

    /// <summary>
    ///     Shared with all loading operations
    /// </summary>
    public interface ILoadingOperationParams
    {
        AsyncLoadProcessReport Report { get; }
    }
}
