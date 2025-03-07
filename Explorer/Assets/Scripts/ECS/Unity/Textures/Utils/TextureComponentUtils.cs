using ECS.StreamableLoading.Textures;
using ECS.Unity.Textures.Components;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.Utils
{
    public static class TextureComponentUtils
    {
        public static bool Equals(in TextureComponent textureComponent, in Promise? promise)
        {
            if (promise == null)
                return false;

            return Equals(textureComponent, promise.Value.LoadingIntention);
        }

        public static bool Equals(in TextureComponent textureComponent, in GetTextureIntention intention) =>
            textureComponent.FileHash == intention.FileHash &&
            textureComponent.Src == intention.Src &&
            textureComponent.WrapMode == intention.WrapMode &&
            textureComponent.FilterMode == intention.FilterMode;
    }
}
