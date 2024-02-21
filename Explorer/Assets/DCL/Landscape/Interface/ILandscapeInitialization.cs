using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System.Threading;

namespace DCL.Landscape.Interface
{
    public interface ILandscapeInitialization
    {
        UniTask InitializeLoadingProgressAsync(AsyncLoadProcessReport loadReport, CancellationToken ct);
    }
}
