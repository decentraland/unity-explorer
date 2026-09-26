using UnityEngine;
using UnityEngine.Pool;

namespace Utility.Primitives
{
    public static class DefaultMaterial
    {
        private static readonly Shader CACHED_SHADER = Shader.Find("DCL/Scene");

        private static readonly ObjectPool<Material> POOL = new (New, defaultCapacity: 1000);

        public static Material Get() =>
            POOL.Get();

        public static void Release(Material material) =>
            POOL.Release(material);

        public static Material New() =>
            new (CACHED_SHADER);
    }
}
