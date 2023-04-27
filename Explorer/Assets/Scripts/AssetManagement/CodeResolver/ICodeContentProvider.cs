using Cysharp.Threading.Tasks;
using System.Threading;

namespace AssetManagement.CodeResolver
{
    public interface ICodeContentProvider
    {
        UniTask<string> GetCodeAsync(string url, CancellationToken cancellationToken = default);
    }
}
