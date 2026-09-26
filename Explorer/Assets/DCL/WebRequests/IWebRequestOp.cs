using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Decorator-like instruction to be able to safely dispose of the request when the continuation is assigned.
    ///     TResult is needed as otherwise a defensive copy of the original structure is always used that prevents writing the result back.
    /// </summary>
    public interface IWebRequestOp<in TWebRequest, TResult> where TWebRequest: struct, ITypedWebRequest
    {
        UniTask<TResult?> ExecuteAsync(TWebRequest webRequest, CancellationToken ct);
    }
}
