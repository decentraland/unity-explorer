using DCL.AvatarRendering.AvatarShape.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    public interface IAvatarMaterialPoolHandler
    {
        bool TryGetMaterialPool(int shaderName, out PoolMaterialSetup setup);
        IReadOnlyCollection<PoolMaterialSetup> GetAllMaterialsPools();
        void Release(AvatarCustomSkinningComponent.MaterialSetup materialSetup);
    }
}
