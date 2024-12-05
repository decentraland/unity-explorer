using Cysharp.Threading.Tasks;

namespace DCL.Caches
{
    public interface IAssetsCache<T> where T: class
    {
        UniTaskVoid PutAsync(string key, T value);

        UniTask<T?> GetAsync(string key);
    }
}
