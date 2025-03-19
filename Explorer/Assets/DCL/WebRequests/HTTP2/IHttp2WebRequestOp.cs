using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.WebRequests.HTTP2
{
    public interface IHttp2WebRequestOp<in TWebRequest, TResult> where TWebRequest: struct, IHttp2TypedWebRequest
    {
        UniTask<TResult?> ExecuteAsync(TWebRequest webRequest, CancellationToken ct);
    }
}
