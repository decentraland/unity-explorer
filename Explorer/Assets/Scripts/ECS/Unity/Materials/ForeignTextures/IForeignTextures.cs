using ECS.Prioritization.Components;
using ECS.Unity.Textures.Components;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.Unity.Materials.ForeignTextures
{
    public interface IForeignTextures
    {
        bool TryCreateGetTexturePromise(
            in TextureComponent? textureComponent,
            ref Promise? promise,
            ref PartitionComponent partitionComponent
        );
    }
}
