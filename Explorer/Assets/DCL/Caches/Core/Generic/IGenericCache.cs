using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Caches.Core.Generic
{
    public interface IGenericCache<T> where T: class
    {
        UniTask PutAsync(string key, T value, CancellationToken token);

        UniTask<T?> GetAsync(string key, CancellationToken token);

        UniTask RemoveAsync(string key, CancellationToken token);
    }
}
