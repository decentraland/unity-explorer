using Arch.Core;
using DCL.SDKComponents.AssetLoad.Components;

namespace DCL.SDKComponents.AssetLoad
{
    public class AssetLoadUtils
    {
        public static void RemoveAssetLoading(World world, Entity loadingEntity, string assetPath, ref AssetLoadComponent existingComponent)
        {
            //TODO: stop each loading properly and then destroy
            world.Destroy(loadingEntity);
            existingComponent.LoadingEntities.Remove(assetPath);
        }
    }
}
