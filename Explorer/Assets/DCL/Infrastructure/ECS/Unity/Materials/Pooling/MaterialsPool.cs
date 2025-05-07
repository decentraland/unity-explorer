#nullable enable

using DCL.AssetsProvision;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace ECS.Unity.Materials.Pooling
{
    public class MaterialsPool : IObjectPool<Material>
    {
        private readonly IObjectPool<Material> pool;

        public MaterialsPool(ProvidedAsset<Material> materialReference, int defaultCapacity = 256, int maxSize = 2048)
        {
            pool = new ObjectPool<Material>(
                () => new Material(materialReference.Value!),
                actionOnDestroy: UnityObjectUtils.SafeDestroy,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );
        }

        public Material Get() =>
            pool.Get()!;

        public PooledObject<Material> Get(out Material v) =>
            pool.Get(out v);

        public void Release(Material element) =>
            pool.Release(element);

        public void Clear() =>
            pool.Clear();

        public int CountInactive => pool.CountInactive;
    }
}
