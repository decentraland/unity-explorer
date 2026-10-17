using DCL.AvatarRendering.AvatarShape.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    public interface IAvatarMaterialPoolHandler
    {
        PoolMaterialSetup GetMaterialPool(int shaderName);
        IReadOnlyCollection<PoolMaterialSetup> GetAllMaterialsPools();
        void Release(AvatarCustomSkinningComponent.MaterialSetup materialSetup);
    }
}
