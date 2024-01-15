using ECS.Prioritization.Components;
using ECS.Unity.Materials.Components;

namespace ECS.Unity.Materials.ForeignTextures
{
    public static class ForeignTexturesExtensions
    {
        private static void CreateGetTexturePromises(this IForeignTextures foreignTextures, ref MaterialComponent materialComponent, ref PartitionComponent partitionComponent)
        {
            foreignTextures.TryCreateGetTexturePromise(in materialComponent.Data.AlbedoTexture, ref materialComponent.AlbedoTexPromise, ref partitionComponent);

            if (materialComponent.Data.IsPbrMaterial)
            {
                foreignTextures.TryCreateGetTexturePromise(in materialComponent.Data.AlphaTexture, ref materialComponent.AlphaTexPromise, ref partitionComponent);
                foreignTextures.TryCreateGetTexturePromise(in materialComponent.Data.EmissiveTexture, ref materialComponent.EmissiveTexPromise, ref partitionComponent);
                foreignTextures.TryCreateGetTexturePromise(in materialComponent.Data.BumpTexture, ref materialComponent.BumpTexPromise, ref partitionComponent);
            }
        }

        public static void StartLoad(this IForeignTextures foreignTextures, ref MaterialComponent materialComponent, ref PartitionComponent partitionComponent)
        {
            foreignTextures.CreateGetTexturePromises(ref materialComponent, ref partitionComponent);
            materialComponent.Status = StreamableLoading.LifeCycle.LoadingInProgress;
        }
    }
}
