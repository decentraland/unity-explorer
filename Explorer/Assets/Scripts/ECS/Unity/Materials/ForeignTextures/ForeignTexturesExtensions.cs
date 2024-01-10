using ECS.Prioritization.Components;
using ECS.Unity.Materials.Components;

namespace ECS.Unity.Materials.ForeignTextures
{
    public static class ForeignTexturesExtensions
    {
        public static void CreateGetTexturePromises(this IForeignTextures foreignTextures, ref MaterialComponent materialComponent, ref PartitionComponent partitionComponent)
        {
            foreignTextures.TryCreateGetTexturePromise(in materialComponent.Data.AlbedoTexture, ref materialComponent.AlbedoTexPromise, ref partitionComponent);

            if (materialComponent.Data.IsPbrMaterial)
            {
                foreignTextures.TryCreateGetTexturePromise(in materialComponent.Data.AlphaTexture, ref materialComponent.AlphaTexPromise, ref partitionComponent);
                foreignTextures.TryCreateGetTexturePromise(in materialComponent.Data.EmissiveTexture, ref materialComponent.EmissiveTexPromise, ref partitionComponent);
                foreignTextures.TryCreateGetTexturePromise(in materialComponent.Data.BumpTexture, ref materialComponent.BumpTexPromise, ref partitionComponent);
            }
        }
    }
}
