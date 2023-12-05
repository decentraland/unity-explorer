using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.AssetsProvision.CodeResolver
{
    public interface IJsCodeProvider
    {
        UniTask<string> GetJsCodeAsync(URLAddress url, CancellationToken cancellationToken = default);
    }
}
