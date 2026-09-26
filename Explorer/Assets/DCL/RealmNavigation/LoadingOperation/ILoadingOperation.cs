using Cysharp.Threading.Tasks;
using DCL.Utilities;
using DCL.Utility.Types;
using System.Threading;

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
