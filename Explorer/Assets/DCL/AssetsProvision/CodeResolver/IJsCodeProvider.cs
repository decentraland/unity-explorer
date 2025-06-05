using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.AssetsProvision.CodeResolver
{
    public interface IJsCodeProvider
    {
        UniTask<string> GetJsCodeAsync(Uri url, CancellationToken cancellationToken);
    }
}
