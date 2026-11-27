using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Utility;

namespace ECS.StreamableLoading.Textures
{
    public static class TexturesExtensions
    {
        public static void TryDereference(this StreamableLoadingResult<SpriteData>.WithFallback? spriteResult)
        {
            spriteResult?.Asset.RemoveReference();
        }

        public static StreamableLoadingResult<SpriteData>.WithFallback ToFullRectSpriteData(this StreamableLoadingResult<TextureData> result, SpriteData fallback, int pixelsPerUnit = 50) =>
            new (
                result.Succeeded
                    ? new SpriteData(result.Asset!,
                        result.Asset!.Asset.Match(_ => fallback, tex2D => Sprite.Create(tex2D, new Rect(0, 0, result.Asset!.Asset.Width, result.Asset.Asset.Height), VectorUtilities.OneHalf, pixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.one, false)))
                    : fallback);

        /// <summary>
        ///     Creates a full-rect sprite from the texture the bundle holds. False when it holds none.
        /// </summary>
        public static bool TryToFullRectSpriteData(this AssetBundleData bundle, out SpriteData spriteData, int pixelsPerUnit = 50)
        {
            if (!bundle.TryGetAsset(out Texture2D texture))
            {
                spriteData = default;
                return false;
            }

            spriteData = new SpriteData(bundle, Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                VectorUtilities.OneHalf, pixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.one, false));

            return true;
        }

        /// <summary>
        ///     Use this for embedded resources that should be never unloaded and participate in the reference counting
        /// </summary>
        public static StreamableLoadingResult<SpriteData>.WithFallback ToUnownedSpriteData(this Sprite sprite) =>
            new (new SpriteData(IStreamableRefCountData.Null.INSTANCE, sprite));

        /// <summary>
        ///     Use this for embedded resources that should be never unloaded and participate in the reference counting
        /// </summary>
        public static SpriteData ToUnownedFulLRectSpriteData(this Texture2D sprite) =>
            new (IStreamableRefCountData.Null.INSTANCE, Sprite.Create(sprite, new Rect(0, 0, sprite.width, sprite.height), VectorUtilities.OneHalf));
    }
}
