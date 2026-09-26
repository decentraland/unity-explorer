using Cysharp.Threading.Tasks;

namespace DCL.ScenesDebug.ScenesConsistency.DelayedResources
{
    public interface IDelayedResource<T>
    {
        UniTask<T> ResourceAsync();

        T DangerousResource();
    }
}
