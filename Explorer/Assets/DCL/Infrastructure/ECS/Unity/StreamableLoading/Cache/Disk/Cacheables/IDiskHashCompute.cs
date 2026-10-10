using DCL.Optimization.Hashing;

namespace ECS.StreamableLoading.Cache.Disk.Cacheables
{
    public interface IDiskHashCompute<TAsset>
    {
        HashKey ComputeHash(in TAsset asset);
    }

    public abstract class AbstractDiskHashCompute<TAsset> : IDiskHashCompute<TAsset>
    {
        public HashKey ComputeHash(in TAsset asset)
        {
            using var _ = HashKeyPayload.NewDiskHashPayload(out var payload);
            FillPayload(payload, asset);
            return payload.NewHashKey();
        }

        protected abstract void FillPayload(IHashKeyPayload keyPayload, in TAsset asset);
    }
}
