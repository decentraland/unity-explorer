using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.AssetsProvision.CodeResolver
{
    public interface IJsCodeProvider
    {
        UniTask<string> GetJsCodeAsync(string url, CancellationToken cancellationToken = default);
    }
}
