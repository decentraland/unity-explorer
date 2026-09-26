using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.MapPins.Components
{
    public struct MapPinComponent
    {
        public Promise? TexturePromise;
        public Vector2Int Position { get; set; }
    }
}
