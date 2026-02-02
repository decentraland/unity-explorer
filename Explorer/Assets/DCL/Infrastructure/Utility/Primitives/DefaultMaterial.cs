using UnityEngine;
using UnityEngine.Pool;

namespace Utility.Primitives
{
    public static class DefaultMaterial
    {
        private static Shader _cachedShader;
        private static ObjectPool<Material> _pool;

        private static Shader CachedShader
        {
            get
            {
                if (_cachedShader == null)
                    _cachedShader = Shader.Find("DCL/Scene");
                return _cachedShader;
            }
        }

        private static ObjectPool<Material> Pool
        {
            get
            {
                if (_pool == null)
                    _pool = new ObjectPool<Material>(New, defaultCapacity: 1000);
                return _pool;
            }
        }

        public static Material Get() =>
            Pool.Get();

        public static void Release(Material material) =>
            Pool.Release(material);

        public static Material New() =>
            new (CachedShader);
    }
}
