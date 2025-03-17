using Arch.Core;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
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

        public static StreamableLoadingResult<SpriteData>.WithFallback ToFullRectSpriteData(this StreamableLoadingResult<Texture2DData> result, SpriteData fallback, int pixelsPerUnit = 50) =>
            new (
                result.Succeeded
                    ? new SpriteData(result.Asset!,
                        Sprite.Create(result.Asset, new Rect(0, 0, result.Asset!.Asset.width, result.Asset.Asset.height), VectorUtilities.OneHalf, pixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.one, false))
                    : fallback);

        public static StreamableLoadingResult<SpriteData>.WithFallback ToFullRectSpriteData(this StreamableLoadingResult<AssetBundleData> result, SpriteData fallback, int pixelsPerUnit = 50)
        {
            if (result.Succeeded)
            {
                Texture2D sprite = result.Asset?.GetMainAsset<Texture2D>()!;

                return new StreamableLoadingResult<SpriteData>.WithFallback(new SpriteData(result.Asset!, Sprite.Create(sprite, new Rect(0, 0, sprite.width, sprite.height),
                    VectorUtilities.OneHalf, pixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.one, false)));
            }

            return new StreamableLoadingResult<SpriteData>.WithFallback(fallback);
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
