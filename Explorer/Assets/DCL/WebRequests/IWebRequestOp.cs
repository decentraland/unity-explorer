using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Decorator-like instruction to be able to safely dispose of the request when the continuation is assigned.
    /// </summary>
    public interface IWebRequestOp<in TWebRequest> where TWebRequest : struct, ITypedWebRequest
    {
        UniTask ExecuteAsync(TWebRequest webRequest, CancellationToken ct);
    }

}
