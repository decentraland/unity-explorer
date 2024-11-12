using ECS.StreamableLoading.Textures;
using ECS.Unity.Textures.Components;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.Utils
{
    public static class TextureComponentUtils
    {
        public static bool Equals(ref TextureComponent textureComponent, ref Promise? promise)
        {
            if (promise == null)
                return false;

            Promise promiseValue = promise.Value;
            GetTextureIntention intention = promiseValue.LoadingIntention;

            return textureComponent.FileHash == promiseValue.LoadingIntention.FileHash &&
                   textureComponent.WrapMode == intention.WrapMode &&
                   textureComponent.FilterMode == intention.FilterMode;
        }
    }
}
