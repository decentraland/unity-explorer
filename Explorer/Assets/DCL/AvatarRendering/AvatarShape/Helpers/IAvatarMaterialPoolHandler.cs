using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    public interface IAvatarMaterialPoolHandler
    {
        PoolMaterialSetup GetMaterialPool(int shaderName);
        Dictionary<int, PoolMaterialSetup>.ValueCollection GetAllMaterialsPools();
        void Release(Material usedMaterial, int poolIndex);
    }
}