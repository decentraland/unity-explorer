using ECS.StreamableLoading.AssetBundles;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    /// <summary>
    ///     Sprite data is a surrogate usually created from <see cref="Texture2DData" />
    ///     but also can be created from the <see cref="AssetBundleData" />
    /// </summary>
    public readonly struct SpriteData
    {
        private readonly IStreamableRefCountData createdFrom;

        public readonly Sprite Sprite;

        public SpriteData(IStreamableRefCountData createdFrom, Sprite sprite)
        {
            this.createdFrom = createdFrom;
            Sprite = sprite;
        }

        public void RemoveReference()
        {
            createdFrom.Dereference();
        }

        public static implicit operator Sprite(SpriteData refCountData) =>
            refCountData.Sprite;
    }
}
