using Arch.Core;
using DCL.ECSComponents;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.MapPins.Components
{
    public struct MapPinComponent
    {
        public Promise? TexturePromise;
        public Texture2D Thumbnail;
        public bool IsDirty { get; set; }
        public Vector2Int Position { get; set; }
    }
}
