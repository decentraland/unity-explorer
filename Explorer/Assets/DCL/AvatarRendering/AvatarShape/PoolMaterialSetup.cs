using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Optimization.Pools;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    public struct PoolMaterialSetup
    {
        public IExtendedObjectPool<Material> Pool;
        public TextureArrayContainer TextureArrayContainer;
    }
}