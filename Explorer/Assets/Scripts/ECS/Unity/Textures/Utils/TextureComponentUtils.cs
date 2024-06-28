using ECS.StreamableLoading.Textures;
using ECS.Unity.Textures.Components;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.VideoPlayer.Utils
{
    public static class TextureComponentUtils
    {
        public static bool Equals(ref TextureComponent textureComponent, ref Promise? promise)
        {
            if (promise == null)
                return false;

            Promise promiseValue = promise.Value;
            GetTextureIntention intention = promiseValue.LoadingIntention;

            return textureComponent.Src == promiseValue.LoadingIntention.CommonArguments.URL &&
                   textureComponent.WrapMode == intention.WrapMode &&
                   textureComponent.FilterMode == intention.FilterMode;
        }
    }
}
