using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Optimization.Pools;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    public readonly struct PoolMaterialSetup
    {
        public readonly IExtendedObjectPool<Material> Pool;
        public readonly TextureArrayContainer TextureArrayContainer;

        public PoolMaterialSetup(IExtendedObjectPool<Material> pool, TextureArrayContainer textureArrayContainer)
        {
            Pool = pool;
            TextureArrayContainer = textureArrayContainer;
        }
    }
}
