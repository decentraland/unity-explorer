using Cysharp.Threading.Tasks;
using System.Threading;

namespace AssetManagement.JsCodeResolver
{
    public interface IJsCodeProvider
    {
        UniTask<string> GetJsCodeAsync(string url, CancellationToken cancellationToken = default);
    }
}
