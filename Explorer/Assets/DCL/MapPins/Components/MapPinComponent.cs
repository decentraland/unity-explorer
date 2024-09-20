using JetBrains.Annotations;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.MapPins.Components
{
    public struct MapPinComponent
    {
        public Promise? TexturePromise;
        [CanBeNull] public Texture2D Thumbnail;

        public bool ThumbnailIsDirty { get; set; }
        public bool IsDirty { get; set; }
        public Vector2Int Position { get; set; }
    }
}
